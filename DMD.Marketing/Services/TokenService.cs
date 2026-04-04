using Microsoft.JSInterop;

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
    private readonly IJSRuntime _js;
    private readonly ILogger<TokenService> _logger;

    // In-memory cache — avoids repeated JS interop within the same circuit.
    private TokenData? _cachedTokens;
    private bool _isInitialized;

    public TokenService(IJSRuntime js, ILogger<TokenService> logger)
    {
        _js = js;
        _logger = logger;
    }

    public async Task SaveTokensAsync(string accessToken, string? refreshToken, int expiresIn)
    {
        try
        {
            var expiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToUnixTimeSeconds();

            await _js.InvokeVoidAsync("localStorage.setItem", "dmd_access_token",  accessToken);
            await _js.InvokeVoidAsync("localStorage.setItem", "dmd_token_expiry",  expiry.ToString());

            if (refreshToken != null)
                await _js.InvokeVoidAsync("localStorage.setItem", "dmd_refresh_token", refreshToken);

            _cachedTokens   = new TokenData { AccessToken = accessToken, RefreshToken = refreshToken };
            _isInitialized  = true;

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

            var accessToken = await _js.InvokeAsync<string?>("localStorage.getItem", "dmd_access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogInformation("No access token found in localStorage");
                return null;
            }

            // Check expiry stored as Unix seconds
            var expiryStr = await _js.InvokeAsync<string?>("localStorage.getItem", "dmd_token_expiry");
            if (long.TryParse(expiryStr, out var expiryUnix))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(expiryUnix);
                if (expiry < DateTimeOffset.UtcNow)
                {
                    _logger.LogInformation("Token expired");
                    await ClearTokensAsync();
                    return null;
                }
            }

            var refreshToken = await _js.InvokeAsync<string?>("localStorage.getItem", "dmd_refresh_token");

            _cachedTokens  = new TokenData { AccessToken = accessToken, RefreshToken = refreshToken };
            _isInitialized = true;

            _logger.LogInformation("Tokens retrieved successfully");
            return _cachedTokens;
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("Cannot access localStorage during prerendering");
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
            await _js.InvokeVoidAsync("localStorage.removeItem", "dmd_access_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "dmd_refresh_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "dmd_token_expiry");
        }
        catch (InvalidOperationException)
        {
            // prerender — nothing to clear in browser
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing tokens");
        }
        finally
        {
            _cachedTokens  = null;
            _isInitialized = false;
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
    public string? access_token  { get; set; }
    public string? token_type    { get; set; }
    public int     expires_in    { get; set; }
    public string? refresh_token { get; set; }
}
