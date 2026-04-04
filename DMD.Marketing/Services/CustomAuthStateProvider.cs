using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace DMD.Marketing.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<CustomAuthStateProvider> _logger;

    // Only cache authenticated state. Anonymous is never cached so every
    // call retries — important because JS interop (ProtectedLocalStorage)
    // may not be ready during OnInitializedAsync of the first circuit render.
    private Task<AuthenticationState>? _cachedAuthStateTask;

    // Tracks whether we previously returned anonymous to any subscriber.
    // Used to fire NotifyAuthenticationStateChanged when we later succeed
    // in reading the token (e.g. in OnAfterRenderAsync after JS is ready).
    private bool _previouslyAnonymous;

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
        
        var state = await LoadAuthenticationStateAsync();

        if (state.User.Identity?.IsAuthenticated == true)
        {
            _cachedAuthStateTask = Task.FromResult(state);

            // If a prior call returned anonymous (JS not ready / prerender),
            // notify all subscribers (AuthorizeView, NavBar, etc.) to re-render.
            if (_previouslyAnonymous)
            {
                _previouslyAnonymous = false;
                _logger.LogInformation("[AuthState] Transitioned from anonymous → authenticated — notifying subscribers");
                NotifyAuthenticationStateChanged(_cachedAuthStateTask);
            }
        }
        else
        {
            // Remember we returned anonymous so we can notify later if needed.
            // Do NOT cache — let the next call retry (JS might not be ready yet).
            _previouslyAnonymous = true;
        }

        return state;
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
                _logger.LogWarning("Invalid JWT token format — token length={Len}, first30={Start}",
                    tokens.AccessToken.Length,
                    tokens.AccessToken[..Math.Min(30, tokens.AccessToken.Length)]);
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
            // JS interop not yet available (prerender or initial circuit render)
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
        _previouslyAnonymous = false;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void ClearCache()
    {
        _logger.LogInformation("[AuthState] Manually clearing auth state cache");
        _cachedAuthStateTask = null;
        _previouslyAnonymous = false;
    }
}
