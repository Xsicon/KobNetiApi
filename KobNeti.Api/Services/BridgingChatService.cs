using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

/// <summary>
/// Forwards chat to a product API when UpstreamApiBaseUrl is configured; otherwise uses the ops store.
/// </summary>
public class BridgingChatService : IChatService
{
    private readonly ChatService _local;
    private readonly UpstreamApiClient _upstream;

    public BridgingChatService(ChatService local, UpstreamApiClient upstream)
    {
        _local = local;
        _upstream = upstream;
    }

    public Task<Response<ChatMessageDTO>> SendMessageAsync(
        string tenantId, SendMessageRequest request, Guid? userId, bool isAdmin)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            var asAdmin = isAdmin || request.SessionId.HasValue;
            return _upstream.ForwardAsync<ChatMessageDTO>(
                tenantId, "api/Chat/send", HttpMethod.Post, request, mintAdminToken: asAdmin);
        }

        return _local.SendMessageAsync(tenantId, request, userId, isAdmin);
    }

    public Task<Response<List<ChatMessageDTO>>> GetMessagesAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            return _upstream.ForwardAsync<List<ChatMessageDTO>>(
                tenantId, $"api/Chat/messages/{sessionId}", HttpMethod.Get, mintAdminToken: true);
        }

        return _local.GetMessagesAsync(tenantId, sessionId, userId, isAdmin);
    }

    public Task<Response<List<ChatSessionDTO>>> GetActiveSessionsAsync(string tenantId)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            return _upstream.ForwardAsync<List<ChatSessionDTO>>(
                tenantId, "api/Chat/active-sessions", HttpMethod.Get, mintAdminToken: true);
        }

        return _local.GetActiveSessionsAsync(tenantId);
    }

    public Task<Response<bool>> CloseSessionAsync(string tenantId, Guid sessionId)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            return _upstream.ForwardAsync<bool>(
                tenantId, $"api/Chat/close/{sessionId}", HttpMethod.Post, mintAdminToken: true);
        }

        return _local.CloseSessionAsync(tenantId, sessionId);
    }

    public Task<Response<string>> GetSessionStatusAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            return _upstream.ForwardAsync<string>(
                tenantId, $"api/Chat/session/{sessionId}/status", HttpMethod.Get, mintAdminToken: isAdmin);
        }

        return _local.GetSessionStatusAsync(tenantId, sessionId, userId, isAdmin);
    }

    public Task<Response<ChatSessionDTO>> GetSessionAsync(string tenantId, Guid sessionId)
    {
        if (_upstream.TryGetUpstream(tenantId, out _, out _))
        {
            return _upstream.ForwardAsync<ChatSessionDTO>(
                tenantId, $"api/Chat/session/{sessionId}", HttpMethod.Get, mintAdminToken: true);
        }

        return _local.GetSessionAsync(tenantId, sessionId);
    }

    public Task<Response<ChatStickyNoteDTO?>> GetStickyNoteAsync(string tenantId, Guid sessionId) =>
        _local.GetStickyNoteAsync(tenantId, sessionId);

    public Task<Response<ChatStickyNoteDTO>> SaveStickyNoteAsync(
        string tenantId, Guid sessionId, SaveChatStickyNoteDTO dto, Guid? updatedBy) =>
        _local.SaveStickyNoteAsync(tenantId, sessionId, dto, updatedBy);

    public Task<Response<bool>> DeleteStickyNoteAsync(string tenantId, Guid sessionId) =>
        _local.DeleteStickyNoteAsync(tenantId, sessionId);
}
