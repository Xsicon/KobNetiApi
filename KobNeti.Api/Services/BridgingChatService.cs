using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

/// <summary>
/// For tenants with UpstreamApiBaseUrl (e.g. muuqwear → MuuqWearApi),
/// forwards Chat calls so Support Hub can see live storefront chats.
/// </summary>
public class BridgingChatService : IChatService
{
    private readonly IChatService _local;
    private readonly UpstreamApiClient _upstream;

    public BridgingChatService(ChatService local, UpstreamApiClient upstream)
    {
        _local = local;
        _upstream = upstream;
    }

    public Task<Response<ChatMessageDTO>> SendMessageAsync(
        string tenantId, SendMessageRequest request, Guid? userId, bool isAdmin)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.SendMessageAsync(tenantId, request, userId, isAdmin);

        // Agent replies from Support Hub always go upstream as admin (session already exists).
        var asAdmin = isAdmin || request.SessionId.HasValue;
        return _upstream.ForwardAsync<ChatMessageDTO>(
            tenantId, "api/Chat/send", HttpMethod.Post, request, mintAdminToken: asAdmin);
    }

    public Task<Response<List<ChatMessageDTO>>> GetMessagesAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.GetMessagesAsync(tenantId, sessionId, userId, isAdmin);

        // Hub agents need admin access to read any session.
        return _upstream.ForwardAsync<List<ChatMessageDTO>>(
            tenantId, $"api/Chat/messages/{sessionId}", HttpMethod.Get, mintAdminToken: true);
    }

    public Task<Response<List<ChatSessionDTO>>> GetActiveSessionsAsync(string tenantId)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.GetActiveSessionsAsync(tenantId);

        return _upstream.ForwardAsync<List<ChatSessionDTO>>(
            tenantId, "api/Chat/active-sessions", HttpMethod.Get, mintAdminToken: true);
    }

    public Task<Response<bool>> CloseSessionAsync(string tenantId, Guid sessionId)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.CloseSessionAsync(tenantId, sessionId);

        return _upstream.ForwardAsync<bool>(
            tenantId, $"api/Chat/close/{sessionId}", HttpMethod.Post, mintAdminToken: true);
    }

    public Task<Response<string>> GetSessionStatusAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.GetSessionStatusAsync(tenantId, sessionId, userId, isAdmin);

        return _upstream.ForwardAsync<string>(
            tenantId, $"api/Chat/session/{sessionId}/status", HttpMethod.Get, mintAdminToken: isAdmin);
    }

    public Task<Response<ChatSessionDTO>> GetSessionAsync(string tenantId, Guid sessionId)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return _local.GetSessionAsync(tenantId, sessionId);

        return _upstream.ForwardAsync<ChatSessionDTO>(
            tenantId, $"api/Chat/session/{sessionId}", HttpMethod.Get, mintAdminToken: true);
    }
}
