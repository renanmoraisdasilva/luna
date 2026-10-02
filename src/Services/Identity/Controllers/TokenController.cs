using System.Security.Claims;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Luna.Identity.Controllers;

[ApiController]
[Consumes("application/x-www-form-urlencoded")]
public sealed class TokenController(
    UserManager<LunaUser> userManager,
    SignInManager<LunaUser> signInManager) : ControllerBase
{
    [HttpPost("~/api/v1/identity/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange(
        [FromForm] TokenRequest form,
        CancellationToken cancellationToken)
    {
        var request = OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext)
            ?? throw new InvalidOperationException("The OpenIddict request cannot be retrieved.");

        if (request.IsPasswordGrantType())
        {
            var user = await userManager.FindByEmailAsync(request.Username!);

            if (user is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
            if (!signIn.Succeeded || await userManager.IsLockedOutAsync(user))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            principal.SetScopes(request.GetScopes());
            principal.SetResources(LunaAuthentication.ApiResource);
            principal.SetClaim(LunaAuthentication.SubjectClaim, user.Id);
            principal.SetClaim(LunaAuthentication.EmailClaim, user.Email ?? string.Empty);
            principal.SetClaim(LunaAuthentication.NameClaim, user.UserName ?? user.Email ?? user.Id);

            foreach (var role in await userManager.GetRolesAsync(user))
            {
                principal.SetClaim(LunaAuthentication.RoleClaim, role);
            }

            principal.SetDestinations(claim => claim.Type switch
            {
                LunaAuthentication.EmailClaim or LunaAuthentication.NameClaim or LunaAuthentication.RoleClaim =>
                    [OpenIddictConstants.Destinations.AccessToken],
                _ => [],
            });

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest(new
                {
                    error = OpenIddictConstants.Errors.InvalidClient,
                    error_description = "A service client ID is required.",
                });
            }

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.SetClaim(OpenIddictConstants.Claims.Subject, request.ClientId);
            identity.SetClaim(OpenIddictConstants.Claims.ClientId, request.ClientId);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());
            principal.SetResources(LunaAuthentication.ApiResource);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new
        {
            error = OpenIddictConstants.Errors.UnsupportedGrantType,
            error_description = "The specified grant type is not supported.",
        });
    }
}

public sealed class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string? GrantType { get; init; }

    [FromForm(Name = "username")]
    public string? Username { get; init; }

    [FromForm(Name = "password")]
    public string? Password { get; init; }

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; init; }

    [FromForm(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromForm(Name = "scope")]
    public string? Scope { get; init; }
}
