using DMD.Marketing.Config;
using DMD.Marketing.Data;
using DMD.Marketing.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── DataProtection — persist keys to DB so they survive container restarts ──
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("DMD.Marketing");

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("local", (sp, client) =>
{
    var config  = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["InternalBaseUrl"] ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout     = TimeSpan.FromSeconds(15);
});

// ── Railway GraphQL API HttpClient ──────────────────────────────────
builder.Services.AddHttpClient("Railway", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var railwayConfig = new RailwayApiConfig();
    config.GetSection("Railway").Bind(railwayConfig);

    client.BaseAddress = new Uri("https://backboard.railway.app/graphql/v2");
    client.Timeout     = TimeSpan.FromSeconds(30);

    if (!string.IsNullOrWhiteSpace(railwayConfig.ApiToken))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", railwayConfig.ApiToken);
});

// ── Configuration binding ───────────────────────────────────────────
builder.Services.Configure<RailwayApiConfig>(builder.Configuration.GetSection("Railway"));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMudServices();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<StripeService>();
builder.Services.AddScoped<AdminClientService>();
builder.Services.AddScoped<IRailwayService, RailwayService>();
builder.Services.AddScoped<TenantMigrationService>();
builder.Services.AddScoped<TenantSeederService>();
builder.Services.AddScoped<ClientActivationService>();
builder.Services.AddHostedService<TrialExpiryBackgroundService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// ── DbContext ──────────────────────────────────────────────────────
// Scoped DbContext for standard DI (controllers, services, etc.)
// Factory for Blazor interactive components that manage their own DbContext lifetime
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    // Retry transient failures (network blips, Postgres restarts) instead of surfacing
    // them to the user. Does not retry auth/permission errors — those aren't transient.
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null));
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
               .EnableLogoutEndpointPassthrough()
               .DisableTransportSecurityRequirement(); // Railway/Cloudflare terminate TLS; internal calls are HTTP
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

// ── Migrate + Seed roles ─────────────────────────────────────────────
// On a cold container start Postgres is frequently not accepting connections yet, so
// transient failures are retried with backoff. Configuration errors (bad password,
// missing database) are never transient — those fail fast with a readable message
// instead of a raw Npgsql stack trace repeated once per restart.
if (!InitializeDatabase(app, connectionString))
    return 1;

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
return 0;

// ── Startup database initialization ──────────────────────────────────
// Returns false when the database could not be prepared; the caller exits non-zero.
static bool InitializeDatabase(WebApplication app, string? connectionString)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_DB_"))
    {
        logger.LogCritical(
            "No database connection string configured. Set the environment variable "
            + "ConnectionStrings__DefaultConnection on this service. The value in "
            + "appsettings.json is a placeholder and is not usable.");
        return false;
    }

    const int maxAttempts = 5;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Database.Migrate();

            // Guard against EF migration-history drift (table/column recorded as applied but DDL never ran).
            // IF NOT EXISTS / ADD COLUMN IF NOT EXISTS are idempotent — safe on every startup.
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
                    "Id"           serial NOT NULL,
                    "FriendlyName" text,
                    "Xml"          text,
                    CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
                );
                ALTER TABLE public."Users" ADD COLUMN IF NOT EXISTS "AppUrl" text;
                ALTER TABLE public."Users" ADD COLUMN IF NOT EXISTS "TenantSlug" varchar(64);
                """);

            foreach (var name in new[] { "Admin", "User" })
            {
                if (!db.Roles.Any(r => r.Name == name))
                    db.Roles.Add(new Role { Name = name, Description = $"{name} role" });
            }
            db.SaveChanges();

            if (attempt > 1)
                logger.LogInformation("Database ready after {Attempts} attempts.", attempt);

            return true;
        }
        catch (PostgresException ex) when (IsConfigurationError(ex))
        {
            // Retrying cannot fix these — the credentials or target database are wrong.
            logger.LogCritical("{Message}\n\n{Target}", DescribeConfigurationError(ex), Describe(connectionString));
            return false;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s, 16s
            logger.LogWarning(
                "Database not reachable (attempt {Attempt}/{Max}): {Error}. Retrying in {Delay}s…",
                attempt, maxAttempts, ex.Message, delay.TotalSeconds);
            Thread.Sleep(delay);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Could not reach the database after {Max} attempts.\n\n{Target}",
                maxAttempts, Describe(connectionString));
            return false;
        }
    }

    return false;
}

// Postgres SQLSTATEs that no amount of retrying will resolve.
static bool IsConfigurationError(PostgresException ex) => ex.SqlState switch
{
    "28P01" => true, // invalid_password
    "28000" => true, // invalid_authorization_specification
    "3D000" => true, // invalid_catalog_name (database does not exist)
    "42501" => true, // insufficient_privilege
    _       => false,
};

static string DescribeConfigurationError(PostgresException ex) => ex.SqlState switch
{
    "28P01" or "28000" =>
        $"Database rejected the credentials ({ex.SqlState}: {ex.MessageText}). "
        + "The host and database resolved, so the connection string is set — the username or "
        + "password is wrong. On Railway this usually means the password was copied literally "
        + "and the Postgres service has since rotated it; use reference variables "
        + "(Password=${{Postgres.PGPASSWORD}}) so it stays in sync. Also check for a ';' or '=' "
        + "in the password, which truncates connection-string parsing unless the value is quoted.",

    "3D000" =>
        $"The target database does not exist ({ex.SqlState}: {ex.MessageText}). "
        + "Check the Database= value in the connection string.",

    "42501" =>
        $"The database user lacks the privileges needed to run migrations ({ex.SqlState}: {ex.MessageText}).",

    _ => $"Database configuration error ({ex.SqlState}: {ex.MessageText}).",
};

// Renders the connection target without leaking the password into logs.
static string Describe(string connectionString)
{
    try
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        return $"Tried: Host={b.Host} Port={b.Port} Database={b.Database} Username={b.Username} Password=***";
    }
    catch
    {
        return "Tried: <connection string could not be parsed — check its syntax>";
    }
}
