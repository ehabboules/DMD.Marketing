using DMD.Marketing.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DMD.Marketing.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserService userService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpPost("/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            var email    = request.Username ?? string.Empty;
            var password = request.Password ?? string.Empty;

            var user = await _userService.ValidateCredentialsAsync(email, password);
            if (user is null)
            {
                var props = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error]            = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The email/password is invalid."
                });
                return Forbid(props, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (!user.IsActive)
            {
                var props = new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error]            = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is inactive."
                });
                return Forbid(props, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            _logger.LogDebug("Login token issued for {Email}", user.Email);

            var identity = new ClaimsIdentity(
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.AddClaim(Claims.Subject, user.Id.ToString());
            identity.AddClaim(Claims.Email,   user.Email);
            identity.AddClaim(Claims.Name,    $"{user.FirstName} {user.LastName}".Trim());

            var userRoles = await _userService.GetUserRolesAsync(user.Id);
            foreach (var ur in userRoles)
                identity.AddClaim(Claims.Role, ur.Role.Name);

            if (user.MustChangePassword)
                identity.AddClaim("must_change_password", "true");

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(new[] { Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess });
            principal.SetDestinations(_ => new[] { Destinations.AccessToken, Destinations.IdentityToken });

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal
                ?? throw new InvalidOperationException("The refresh token is invalid.");
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }
}
