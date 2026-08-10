using Sominnercore.SupportApi.DTOs;
using Sominnercore.SupportApi.Shared;

namespace Sominnercore.SupportApi.Services;

public class BridgingSupportCountsService : ISupportCountsService
{
    private readonly SupportCountsService _local;
    private readonly UpstreamApiClient _upstream;

    public BridgingSupportCountsService(SupportCountsService local, UpstreamApiClient upstream)
    {
        _local = local;
        _upstream = upstream;
    }

    public async Task<Response<SupportCountsDTO>> GetCountsAsync(string tenantId)
    {
        if (!_upstream.TryGetUpstream(tenantId, out _, out _))
            return await _local.GetCountsAsync(tenantId);

        var sessions = await _upstream.ForwardAsync<List<ChatSessionDTO>>(
            tenantId, "api/Chat/active-sessions", HttpMethod.Get, mintAdminToken: true);
        var stats = await _upstream.ForwardAsync<TicketStatsDTO>(
            tenantId, "api/Help/admin/stats", HttpMethod.Get, mintAdminToken: true);

        if (!sessions.Success && !stats.Success)
        {
            return Response<SupportCountsDTO>.Fail(
                sessions.Message != string.Empty ? sessions.Message : stats.Message);
        }

        var activeChats = sessions.Success ? sessions.Data?.Count ?? 0 : 0;
        var openTickets = stats.Success
            ? (stats.Data?.OpenCount ?? 0) + (stats.Data?.InProgressCount ?? 0)
            : 0;

        return Response<SupportCountsDTO>.SuccessResponse(new SupportCountsDTO
        {
            ActiveChats = activeChats,
            OpenTickets = openTickets
        }, "Counts loaded from upstream");
    }
}
