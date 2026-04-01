using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace DMD.Marketing.Services;

public interface ITokenService
{
    Task SaveTokensAsync(string accessToken, string? refreshToken, int expiresIn);
    Task<TokenData?> GetTokensAsync();
    Task ClearTokensAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class TokenService : ITokenService
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ILogger<TokenService> _logger;

    // In-memory cache to avoid JS interop during prerender
    private TokenData? _cachedTokens;
    private bool _isInitialized = false;

    public TokenService(ProtectedLocalStorage localStorage, ILogger<TokenService> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task SaveTokensAsync(string accessToken, string? refreshToken, int expiresIn)
    {
        try
        {
            await _localStorage.SetAsync("access_token", accessToken);

            if (refreshToken != null)
                await _localStorage.SetAsync("refresh_token", refreshToken);

            await _localStorage.SetAsync("token_expiry", DateTime.UtcNow.AddSeconds(expiresIn));

            _cachedTokens = new TokenData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
            _isInitialized = true;

            _logger.LogInformation("Tokens saved successfully");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot save tokens during prerendering: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens");
            throw;
        }
    }

    public async Task<TokenData?> GetTokensAsync()
    {
        try
        {
            if (_isInitialized && _cachedTokens != null)
            {
                _logger.LogDebug("Returning cached tokens");
                return _cachedTokens;
            }

            var accessTokenResult = await _localStorage.GetAsync<string>("access_token");

            if (!accessTokenResult.Success || string.IsNullOrEmpty(accessTokenResult.Value))
            {
                _logger.LogInformation("No access token found");
                return null;
            }

            var refreshTokenResult = await _localStorage.GetAsync<string>("refresh_token");
            var expiryResult = await _localStorage.GetAsync<DateTime>("token_expiry");

            if (expiryResult.Success && expiryResult.Value < DateTime.UtcNow)
            {
                _logger.LogInformation("Token expired");
                await ClearTokensAsync();
                return null;
            }

            _logger.LogInformation("Tokens retrieved successfully");

            _cachedTokens = new TokenData
            {
                AccessToken = accessTokenResult.Value,
                RefreshToken = refreshTokenResult.Success ? refreshTokenResult.Value : null
            };
            _isInitialized = true;

            return _cachedTokens;
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("Cannot access storage during prerendering");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tokens");
            return null;
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            await _localStorage.DeleteAsync("access_token");
            await _localStorage.DeleteAsync("refresh_token");
            await _localStorage.DeleteAsync("token_expiry");

            _cachedTokens = null;
            _isInitialized = false;

            _logger.LogInformation("Tokens cleared");
        }
        catch (InvalidOperationException)
        {
            _cachedTokens = null;
            _isInitialized = false;
            _logger.LogDebug("Cleared token cache during prerendering");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tokens");
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var tokens = await GetTokensAsync();
        return tokens != null && !string.IsNullOrEmpty(tokens.AccessToken);
    }
}

public class TokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
}

public class TokenResponse
{
    public string? access_token { get; set; }
    public string? token_type { get; set; }
    public int expires_in { get; set; }
    public string? refresh_token { get; set; }
}
