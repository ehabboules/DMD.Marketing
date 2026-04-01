using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace DMD.Marketing.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<CustomAuthStateProvider> _logger;

    private Task<AuthenticationState>? _cachedAuthStateTask;

    public CustomAuthStateProvider(ITokenService tokenService, ILogger<CustomAuthStateProvider> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cachedAuthStateTask != null)
        {
            _logger.LogDebug("[AuthState] Returning cached authentication state");
            return await _cachedAuthStateTask;
        }

        _cachedAuthStateTask = LoadAuthenticationStateAsync();
        return await _cachedAuthStateTask;
    }

    private async Task<AuthenticationState> LoadAuthenticationStateAsync()
    {
        var anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        try
        {
            var tokens = await _tokenService.GetTokensAsync();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                _logger.LogInformation("No tokens found - user is anonymous");
                return anonymous;
            }

            _logger.LogDebug("Token found, parsing JWT");
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(tokens.AccessToken))
            {
                _logger.LogWarning("Invalid JWT token format");
                await _tokenService.ClearTokensAsync();
                return anonymous;
            }

            var jwtToken = handler.ReadJwtToken(tokens.AccessToken);

            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                _logger.LogInformation("Token expired");
                await _tokenService.ClearTokensAsync();
                return anonymous;
            }

            // Map OpenIddict claims to standard ClaimTypes
            var claims = jwtToken.Claims.Select(c => c.Type switch
            {
                OpenIddictConstants.Claims.Subject => new Claim(ClaimTypes.NameIdentifier, c.Value),
                OpenIddictConstants.Claims.Name    => new Claim(ClaimTypes.Name, c.Value),
                OpenIddictConstants.Claims.Email   => new Claim(ClaimTypes.Email, c.Value),
                OpenIddictConstants.Claims.Role    => new Claim(ClaimTypes.Role, c.Value),
                _ => c
            }).ToList();

            var identity = new ClaimsIdentity(
                claims,
                "jwt",
                ClaimTypes.Name,
                ClaimTypes.Role);

            var user = new ClaimsPrincipal(identity);
            _logger.LogInformation("User authenticated: {UserName}", user.Identity?.Name);

            return new AuthenticationState(user);
        }
        catch (InvalidOperationException)
        {
            _logger.LogInformation("Cannot access storage during prerendering - returning anonymous");
            return anonymous;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error loading state");
            return anonymous;
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        _logger.LogInformation("[AuthState] Authentication state changed - clearing cache");
        _cachedAuthStateTask = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void ClearCache()
    {
        _logger.LogInformation("[AuthState] Manually clearing auth state cache");
        _cachedAuthStateTask = null;
    }
}
