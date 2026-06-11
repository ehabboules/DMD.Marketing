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
builder.Services.AddHttpClient("local", (sp, client) =>
{
    var config  = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["InternalBaseUrl"] ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromSeconds(15);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMudServices();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<StripeService>();
builder.Services.AddHostedService<TrialExpiryBackgroundService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// ── DbContext ──────────────────────────────────────────────────────
// Scoped DbContext for standard DI (controllers, services, etc.)
// Factory for Blazor interactive components that manage their own DbContext lifetime
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseOpenIddict();
}, ServiceLifetime.Scoped);

// ── Password hasher ────────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ── Cookie authentication ──────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath           = "/login";
        options.AccessDeniedPath    = "/login";
        options.ExpireTimeSpan      = TimeSpan.FromDays(14);
        options.SlidingExpiration   = true;
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Strict;
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
               .AddEphemeralSigningKey()
               .DisableAccessTokenEncryption(); // tokens are JWS (signed-only), readable by JwtSecurityTokenHandler

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

// ── Migrate + Seed roles ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
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

// ── Security response headers ──────────────────────────────────────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"]           = "SAMEORIGIN";
    ctx.Response.Headers["X-Content-Type-Options"]    = "nosniff";
    ctx.Response.Headers["Referrer-Policy"]           = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"]        = "camera=(), microphone=(), geolocation=()";
    // In development allow localhost for VS Browser Link + Hot Reload SignalR
    var connectSrc = app.Environment.IsDevelopment()
        ? "connect-src 'self' wss: ws: http://localhost:* https://localhost:* api.stripe.com; "
        : "connect-src 'self' wss: api.stripe.com; ";
    ctx.Response.Headers["Content-Security-Policy"]   =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' js.stripe.com static.cloudflareinsights.com; " +
        "style-src 'self' 'unsafe-inline' fonts.googleapis.com; " +
        "font-src 'self' fonts.gstatic.com; " +
        connectSrc +
        "frame-src js.stripe.com hooks.stripe.com; " +
        "frame-ancestors 'self'; " +
        "img-src 'self' data:;";
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<DMD.Marketing.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();
