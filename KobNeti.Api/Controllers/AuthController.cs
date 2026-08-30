using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Email;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Controllers;

[Route("api/Auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IPasswordResetService _resets;
    private readonly IConfiguration _config;

    public AuthController(IPasswordResetService resets, IConfiguration config)
    {
        _resets = resets;
        _config = config;
    }

    /// <summary>
    /// Request a password-reset email via Postmark (Supabase recovery link).
    /// No tenant header required. Always returns a generic success message when accepted.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<Response<object>>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        var fallbackRedirect = (_config["Auth:PasswordResetRedirectUrl"] ?? "").Trim();
        var redirectTo = string.IsNullOrWhiteSpace(request.RedirectTo)
            ? fallbackRedirect
            : request.RedirectTo.Trim();

        if (string.IsNullOrWhiteSpace(redirectTo))
            redirectTo = "https://localhost/admin/reset-password";

        var result = await _resets.RequestResetAsync(request.Email ?? "", redirectTo, ct);
        if (!result.Accepted)
            return BadRequest(Response<object>.Fail(result.Message));

        return Ok(Response<object>.SuccessResponse(new
        {
            emailed = result.EmailQueued
        }, result.Message));
    }
}
