using Luna.Identity.Application;
using Luna.Contracts.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace Luna.Identity.Controllers;

[ApiController]
[Route("api/v1/identity")]
public sealed class AccountController(
    RegisterCustomerHandler registerCustomer,
    UserManager<LunaUser> userManager) : ControllerBase
{
    [HttpPost("register-profile")]
    public async Task<IActionResult> Register(RegisterProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await registerCustomer.HandleAsync(new RegisterCustomerCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName), cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(error => new { code = error.Code, description = error.Description }),
            });
        }

        return Created($"/api/v1/identity/users/{result.UserId}", null);
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return user is null ? Unauthorized() : Ok(ToProfileResponse(user));
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateCurrentProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(error => new { code = error.Code, description = error.Description }),
            });
        }

        return Ok(ToProfileResponse(user));
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangeCurrentPassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(error => new { code = error.Code, description = error.Description }),
            });
        }

        return NoContent();
    }

    private async Task<LunaUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(LunaAuthentication.SubjectClaim) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await userManager.FindByIdAsync(userId);
    }

    private static ProfileResponse ToProfileResponse(LunaUser user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FirstName,
        user.LastName);

    public sealed record RegisterProfileRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName);

    public sealed record UpdateProfileRequest(string FirstName, string LastName);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public sealed record ProfileResponse(string Id, string Email, string FirstName, string LastName);
}
