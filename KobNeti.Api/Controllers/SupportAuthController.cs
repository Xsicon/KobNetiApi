using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Controllers;

[Route("api/SupportAuth")]
[ApiController]
public class SupportAuthController : ControllerBase
{
    private readonly IAgentTokenService _tokens;

    public SupportAuthController(IAgentTokenService tokens) => _tokens = tokens;

    [AllowAnonymous]
    [HttpPost("exchange")]
    public async Task<ActionResult<Response<AgentTokenResponse>>> Exchange([FromBody] ExchangeTokenRequest request)
    {
        var (ok, token, error) = await _tokens.ExchangeSupabaseTokenAsync(request.AccessToken);
        if (!ok || token is null)
            return Unauthorized(Response<AgentTokenResponse>.Fail(error ?? "Unauthorized"));

        return Ok(Response<AgentTokenResponse>.SuccessResponse(new AgentTokenResponse
        {
            AccessToken = token,
            ExpiresAt = DateTime.UtcNow.AddHours(12)
        }, "Token issued"));
    }
}
