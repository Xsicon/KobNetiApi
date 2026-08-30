using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

/// <summary>
/// Chat escalations create ops-owned incidents locally even when chat sessions live upstream.
/// </summary>
public class BridgingIncidentService : IIncidentService
{
    private readonly IncidentService _local;
    private readonly UpstreamApiClient _upstream;

    public BridgingIncidentService(IncidentService local, UpstreamApiClient upstream)
    {
        _local = local;
        _upstream = upstream;
    }

    private bool UseUpstream(string tenantId) =>
        _upstream.TryGetUpstream(tenantId, out _, out _);

    public Task<Response<List<IncidentDTO>>> ListAsync(string tenantId, string? status, int page, int pageSize) =>
        _local.ListAsync(tenantId, status, page, pageSize);

    public Task<Response<IncidentDTO>> GetAsync(string tenantId, Guid id) =>
        _local.GetAsync(tenantId, id);

    public Task<Response<IncidentDTO>> CreateAsync(
        string tenantId, CreateIncidentDTO request, string? actorName, Guid? actorUserId) =>
        _local.CreateAsync(tenantId, request, actorName, actorUserId);

    public Task<Response<IncidentDTO>> EscalateFromTicketAsync(
        string tenantId, Guid ticketId, EscalateTicketDTO request, string? actorName, Guid? actorUserId) =>
        _local.EscalateFromTicketAsync(tenantId, ticketId, request, actorName, actorUserId);

    public async Task<Response<IncidentDTO>> EscalateFromChatAsync(
        string tenantId, Guid sessionId, EscalateFromChatDTO request, string? actorName, Guid? actorUserId)
    {
        if (!UseUpstream(tenantId))
            return await _local.EscalateFromChatAsync(tenantId, sessionId, request, actorName, actorUserId);

        var sessionRes = await _upstream.ForwardAsync<ChatSessionDTO>(
            tenantId, $"api/Chat/session/{sessionId}", HttpMethod.Get, mintAdminToken: true);
        if (!sessionRes.Success || sessionRes.Data is null)
        {
            return Response<IncidentDTO>.Fail(
                string.IsNullOrWhiteSpace(sessionRes.Message) ? "Chat session not found" : sessionRes.Message);
        }

        var messagesRes = await _upstream.ForwardAsync<List<ChatMessageDTO>>(
            tenantId, $"api/Chat/messages/{sessionId}", HttpMethod.Get, mintAdminToken: true);
        var messages = messagesRes.Success && messagesRes.Data is not null
            ? messagesRes.Data
            : [];

        return await _local.EscalateFromChatAsync(
            tenantId, sessionId, sessionRes.Data, messages, request, actorName, actorUserId);
    }

    public Task<Response<IncidentDTO>> UpdateAsync(
        string tenantId, Guid id, UpdateIncidentDTO request, string? actorName) =>
        _local.UpdateAsync(tenantId, id, request, actorName);
}
