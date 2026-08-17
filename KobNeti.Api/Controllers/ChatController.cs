using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Services;
using KobNeti.Api.Shared;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Controllers;

[Route("api/[controller]")]
public class ChatController : ApiControllerBase
{
    private readonly IChatService _chatService;
    private readonly ITenantContextAccessor _tenant;

    public ChatController(IChatService chatService, ITenantContextAccessor tenant)
    {
        _chatService = chatService;
        _tenant = tenant;
    }

    [AllowAnonymous]
    [HttpPost("send")]
    public async Task<ActionResult<Response<ChatMessageDTO>>> SendMessage([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(Response<ChatMessageDTO>.Fail("Message cannot be empty"));

        var userId = AdminRoleClaims.GetUserId(User);
        var isAdmin = User.Identity?.IsAuthenticated == true && AdminRoleClaims.CanActAsChatAdmin(User);

        if (!isAdmin && !userId.HasValue && string.IsNullOrWhiteSpace(request.GuestName))
            return BadRequest(Response<ChatMessageDTO>.Fail("Guest name is required"));

        if (!isAdmin && !userId.HasValue && !request.SessionId.HasValue && string.IsNullOrWhiteSpace(request.GuestEmail))
            return BadRequest(Response<ChatMessageDTO>.Fail("Guest email is required"));

        var result = await _chatService.SendMessageAsync(RequireTenantId(_tenant), request, userId, isAdmin);
        if (!result.Success && result.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, result);
        return HandleResponse(result);
    }

    [AllowAnonymous]
    [HttpGet("messages/{sessionId:guid}")]
    public async Task<ActionResult<Response<List<ChatMessageDTO>>>> GetMessages(Guid sessionId)
    {
        var userId = AdminRoleClaims.GetUserId(User);
        var isAdmin = User.Identity?.IsAuthenticated == true && AdminRoleClaims.CanActAsChatAdmin(User);
        var result = await _chatService.GetMessagesAsync(RequireTenantId(_tenant), sessionId, userId, isAdmin);
        if (!result.Success && result.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, result);
        return HandleResponse(result);
    }

    [HttpGet("active-sessions")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<List<ChatSessionDTO>>>> GetActiveSessions()
    {
        var result = await _chatService.GetActiveSessionsAsync(RequireTenantId(_tenant));
        return HandleResponse(result);
    }

    [HttpPost("close/{sessionId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<bool>>> CloseSession(Guid sessionId)
    {
        var result = await _chatService.CloseSessionAsync(RequireTenantId(_tenant), sessionId);
        return HandleResponse(result);
    }

    [AllowAnonymous]
    [HttpGet("session/{sessionId:guid}/status")]
    public async Task<ActionResult<Response<string>>> GetSessionStatus(Guid sessionId)
    {
        var userId = AdminRoleClaims.GetUserId(User);
        var isAdmin = User.Identity?.IsAuthenticated == true && AdminRoleClaims.CanActAsChatAdmin(User);
        var result = await _chatService.GetSessionStatusAsync(RequireTenantId(_tenant), sessionId, userId, isAdmin);
        if (!result.Success && result.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, result);
        return HandleResponse(result);
    }

    [HttpGet("session/{sessionId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<ChatSessionDTO>>> GetSession(Guid sessionId)
    {
        var result = await _chatService.GetSessionAsync(RequireTenantId(_tenant), sessionId);
        return HandleResponse(result);
    }
}
