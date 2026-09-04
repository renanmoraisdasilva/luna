using System.Security.Claims;
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
            if (user is null || !await signInManager.UserManager.CheckPasswordAsync(user, request.Password!))
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            principal.SetScopes(request.GetScopes());
            principal.SetResources("luna-api");
            principal.SetClaim(OpenIddictConstants.Claims.Subject, user.Id);
            principal.SetClaim(OpenIddictConstants.Claims.Email, user.Email ?? string.Empty);
            principal.SetClaim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Email ?? user.Id);

            foreach (var role in await userManager.GetRolesAsync(user))
            {
                principal.SetClaim(OpenIddictConstants.Claims.Role, role);
            }

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