using Sominnercore.SupportApi.DTOs;
using Sominnercore.SupportApi.Shared;

namespace Sominnercore.SupportApi.Services;

/// <summary>
/// Proxies Help ticket/KB admin reads to upstream (MuuqWearApi) when configured.
/// </summary>
public class BridgingHelpService : IHelpService
{
    private readonly HelpService _local;
    private readonly UpstreamApiClient _upstream;

    public BridgingHelpService(HelpService local, UpstreamApiClient upstream)
    {
        _local = local;
        _upstream = upstream;
    }

    private bool UseUpstream(string tenantId) =>
        _upstream.TryGetUpstream(tenantId, out _, out _);

    public Task<Response<SupportTicketDTO>> SubmitTicketAsync(string tenantId, SubmitTicketDTO request) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketDTO>(tenantId, "api/Help/ticket", HttpMethod.Post, request, mintAdminToken: false)
            : _local.SubmitTicketAsync(tenantId, request);

    public Task<Response<PaginatedResponse<SupportTicketDTO>>> GetAllTicketsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        if (!UseUpstream(tenantId))
            return _local.GetAllTicketsAsync(tenantId, status, page, pageSize);

        var qs = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
            qs += $"&status={Uri.EscapeDataString(status)}";
        return _upstream.ForwardAsync<PaginatedResponse<SupportTicketDTO>>(
            tenantId, $"api/Help/admin/tickets?{qs}", HttpMethod.Get);
    }

    public Task<Response<SupportTicketDTO>> GetTicketByIdAsync(string tenantId, Guid ticketId) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketDTO>(tenantId, $"api/Help/admin/tickets/{ticketId}", HttpMethod.Get)
            : _local.GetTicketByIdAsync(tenantId, ticketId);

    public Task<Response<SupportTicketDTO>> UpdateTicketStatusAsync(string tenantId, Guid ticketId, string status) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketDTO>(
                tenantId, $"api/Help/admin/tickets/{ticketId}/status", HttpMethod.Patch, new UpdateTicketStatusDTO { Status = status })
            : _local.UpdateTicketStatusAsync(tenantId, ticketId, status);

    public Task<Response<SupportTicketDTO>> UpdateTicketAsync(string tenantId, Guid ticketId, UpdateTicketDTO request) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketDTO>(
                tenantId, $"api/Help/admin/tickets/{ticketId}", HttpMethod.Patch, request)
            : _local.UpdateTicketAsync(tenantId, ticketId, request);

    public Task<Response<SupportTicketReplyDTO>> AddTicketReplyAsync(
        string tenantId, Guid ticketId, Guid? senderId, string senderName, string message) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketReplyDTO>(
                tenantId, $"api/Help/admin/tickets/{ticketId}/replies", HttpMethod.Post,
                new AddTicketReplyDTO { Message = message })
            : _local.AddTicketReplyAsync(tenantId, ticketId, senderId, senderName, message);

    public Task<Response<SupportTicketDTO>> AssignTicketToMeAsync(
        string tenantId, Guid ticketId, Guid userId, string userName) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<SupportTicketDTO>(
                tenantId, $"api/Help/admin/tickets/{ticketId}/assign-me", HttpMethod.Post)
            : _local.AssignTicketToMeAsync(tenantId, ticketId, userId, userName);

    public async Task<Response<TicketStatsDTO>> GetTicketStatsAsync(string tenantId)
    {
        if (!UseUpstream(tenantId))
            return await _local.GetTicketStatsAsync(tenantId);

        var upstream = await _upstream.ForwardAsync<TicketStatsDTO>(
            tenantId, "api/Help/admin/stats", HttpMethod.Get);
        return upstream.Success ? upstream : await _local.GetTicketStatsAsync(tenantId);
    }

    public Task<Response<PaginatedResponse<HelpArticleDTO>>> GetPublishedArticlesAsync(
        string tenantId, string? category, string? search, int page, int pageSize)
    {
        if (!UseUpstream(tenantId))
            return _local.GetPublishedArticlesAsync(tenantId, category, search, page, pageSize);

        var qs = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category)) qs += $"&category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&search={Uri.EscapeDataString(search)}";
        return _upstream.ForwardAsync<PaginatedResponse<HelpArticleDTO>>(
            tenantId, $"api/Help/articles?{qs}", HttpMethod.Get, mintAdminToken: false);
    }

    public Task<Response<HelpArticleDTO>> GetPublishedArticleAsync(string tenantId, Guid articleId) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleDTO>(
                tenantId, $"api/Help/articles/{articleId}", HttpMethod.Get, mintAdminToken: false)
            : _local.GetPublishedArticleAsync(tenantId, articleId);

    public Task<Response<PaginatedResponse<HelpArticleDTO>>> GetAdminArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize)
    {
        if (!UseUpstream(tenantId))
            return _local.GetAdminArticlesAsync(tenantId, category, status, search, page, pageSize);

        var qs = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category)) qs += $"&category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrWhiteSpace(status)) qs += $"&status={Uri.EscapeDataString(status)}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&search={Uri.EscapeDataString(search)}";
        return _upstream.ForwardAsync<PaginatedResponse<HelpArticleDTO>>(
            tenantId, $"api/Help/admin/articles?{qs}", HttpMethod.Get);
    }

    public Task<Response<HelpArticleDTO>> GetAdminArticleAsync(string tenantId, Guid articleId, string? voterKey) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleDTO>(tenantId, $"api/Help/admin/articles/{articleId}", HttpMethod.Get)
            : _local.GetAdminArticleAsync(tenantId, articleId, voterKey);

    public Task<Response<HelpArticleDTO>> CreateArticleAsync(string tenantId, SaveHelpArticleDTO request) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleDTO>(tenantId, "api/Help/admin/articles", HttpMethod.Post, request)
            : _local.CreateArticleAsync(tenantId, request);

    public Task<Response<HelpArticleDTO>> UpdateArticleAsync(string tenantId, Guid articleId, SaveHelpArticleDTO request) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleDTO>(
                tenantId, $"api/Help/admin/articles/{articleId}", HttpMethod.Put, request)
            : _local.UpdateArticleAsync(tenantId, articleId, request);

    public Task<Response<HelpArticleDTO>> UpdateArticleStatusAsync(string tenantId, Guid articleId, string status) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleDTO>(
                tenantId, $"api/Help/admin/articles/{articleId}/status", HttpMethod.Patch,
                new UpdateHelpArticleStatusDTO { Status = status })
            : _local.UpdateArticleStatusAsync(tenantId, articleId, status);

    public Task<Response<bool>> DeleteArticleAsync(string tenantId, Guid articleId) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<bool>(tenantId, $"api/Help/admin/articles/{articleId}", HttpMethod.Delete)
            : _local.DeleteArticleAsync(tenantId, articleId);

    public Task<Response<string>> UploadImageAsync(
        string tenantId, Guid? createdBy, string fileName, string contentType, Stream content, long length) =>
        // Multipart upload bridge is out of scope for v1 bridge — keep local metadata path.
        _local.UploadImageAsync(tenantId, createdBy, fileName, contentType, content, length);

    public Task<Response<HelpArticleCommentDTO>> AddCommentAsync(
        string tenantId, Guid articleId, string authorName, string body) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleCommentDTO>(
                tenantId, $"api/Help/admin/articles/{articleId}/comments", HttpMethod.Post,
                new AddHelpArticleCommentDTO { Body = body })
            : _local.AddCommentAsync(tenantId, articleId, authorName, body);

    public Task<Response<HelpArticleEngagementDTO>> VoteAsync(
        string tenantId, Guid articleId, string voterKey, string vote) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleEngagementDTO>(
                tenantId, $"api/Help/admin/articles/{articleId}/vote", HttpMethod.Post,
                new HelpArticleVoteRequestDTO { Vote = vote })
            : _local.VoteAsync(tenantId, articleId, voterKey, vote);

    public Task<Response<HelpArticleEngagementDTO>> GetEngagementAsync(
        string tenantId, Guid articleId, string? voterKey) =>
        UseUpstream(tenantId)
            ? _upstream.ForwardAsync<HelpArticleEngagementDTO>(
                tenantId, $"api/Help/admin/articles/{articleId}/engagement", HttpMethod.Get)
            : _local.GetEngagementAsync(tenantId, articleId, voterKey);
}
