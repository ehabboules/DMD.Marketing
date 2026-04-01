using DMD.Marketing.Data;
using DMD.Marketing.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMudServices();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// ── DbContext ──────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseOpenIddict();
});

// ── Password hasher ────────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ── Cookie authentication ──────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/login";
        options.AccessDeniedPath  = "/login";
        options.ExpireTimeSpan    = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ── OpenIddict ─────────────────────────────────────────────────────
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo")
               .SetLogoutEndpointUris("/connect/logout");

        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowPasswordFlow()
               .AllowRefreshTokenFlow();

        options.AcceptAnonymousClients();

        options.RegisterScopes("openid", "profile", "email", "offline_access");

        options.AddEphemeralEncryptionKey()
               .AddEphemeralSigningKey();

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough()
               .EnableUserinfoEndpointPassthrough()
               .EnableLogoutEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

// ── Seed roles ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    foreach (var name in new[] { "Admin", "User" })
    {
        if (!db.Roles.Any(r => r.Name == name))
            db.Roles.Add(new Role { Name = name, Description = $"{name} role" });
    }
    db.SaveChanges();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<DMD.Marketing.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
