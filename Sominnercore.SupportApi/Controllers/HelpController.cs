using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sominnercore.SupportApi.Auth;
using Sominnercore.SupportApi.DTOs;
using Sominnercore.SupportApi.Services;
using Sominnercore.SupportApi.Shared;
using Sominnercore.SupportApi.Tenancy;

namespace Sominnercore.SupportApi.Controllers;

[Route("api/[controller]")]
public class HelpController : ApiControllerBase
{
    private readonly IHelpService _help;
    private readonly ITenantContextAccessor _tenant;

    public HelpController(IHelpService help, ITenantContextAccessor tenant)
    {
        _help = help;
        _tenant = tenant;
    }

    [HttpPost("ticket")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<SupportTicketDTO>>> SubmitTicket([FromBody] SubmitTicketDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(Response<SupportTicketDTO>.Fail("Name is required"));
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest(Response<SupportTicketDTO>.Fail("Email is required"));
        if (string.IsNullOrWhiteSpace(request.Category)) return BadRequest(Response<SupportTicketDTO>.Fail("Category is required"));
        if (string.IsNullOrWhiteSpace(request.Subject)) return BadRequest(Response<SupportTicketDTO>.Fail("Subject is required"));
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(Response<SupportTicketDTO>.Fail("Message is required"));
        return HandleResponse(await _help.SubmitTicketAsync(RequireTenantId(_tenant), request));
    }

    [HttpGet("admin/tickets")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<PaginatedResponse<SupportTicketDTO>>>> GetAllTickets(
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        return HandleResponse(await _help.GetAllTicketsAsync(RequireTenantId(_tenant), status, page, pageSize));
    }

    [HttpGet("admin/tickets/{ticketId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<SupportTicketDTO>>> GetTicketById(Guid ticketId) =>
        HandleResponse(await _help.GetTicketByIdAsync(RequireTenantId(_tenant), ticketId));

    [HttpPatch("admin/tickets/{ticketId:guid}/status")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<SupportTicketDTO>>> UpdateTicketStatus(
        Guid ticketId, [FromBody] UpdateTicketStatusDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            return BadRequest(Response<SupportTicketDTO>.Fail("Status is required"));
        return HandleResponse(await _help.UpdateTicketStatusAsync(RequireTenantId(_tenant), ticketId, request.Status));
    }

    [HttpPatch("admin/tickets/{ticketId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<SupportTicketDTO>>> UpdateTicket(
        Guid ticketId, [FromBody] UpdateTicketDTO request) =>
        HandleResponse(await _help.UpdateTicketAsync(RequireTenantId(_tenant), ticketId, request));

    [HttpPost("admin/tickets/{ticketId:guid}/replies")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<SupportTicketReplyDTO>>> AddTicketReply(
        Guid ticketId, [FromBody] AddTicketReplyDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(Response<SupportTicketReplyDTO>.Fail("Message is required"));
        return HandleResponse(await _help.AddTicketReplyAsync(
            RequireTenantId(_tenant), ticketId, AdminRoleClaims.GetUserId(User),
            AdminRoleClaims.GetDisplayName(User), request.Message));
    }

    [HttpPost("admin/tickets/{ticketId:guid}/assign-me")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<SupportTicketDTO>>> AssignMe(Guid ticketId)
    {
        var userId = AdminRoleClaims.GetUserId(User) ?? Guid.Empty;
        return HandleResponse(await _help.AssignTicketToMeAsync(
            RequireTenantId(_tenant), ticketId, userId, AdminRoleClaims.GetDisplayName(User)));
    }

    [HttpGet("admin/stats")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<TicketStatsDTO>>> GetStats() =>
        HandleResponse(await _help.GetTicketStatsAsync(RequireTenantId(_tenant)));

    [HttpGet("articles")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<PaginatedResponse<HelpArticleDTO>>>> GetArticles(
        [FromQuery] string? category = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        return HandleResponse(await _help.GetPublishedArticlesAsync(RequireTenantId(_tenant), category, search, page, pageSize));
    }

    [HttpGet("articles/{articleId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<Response<HelpArticleDTO>>> GetArticle(Guid articleId) =>
        HandleResponse(await _help.GetPublishedArticleAsync(RequireTenantId(_tenant), articleId));

    [HttpGet("admin/articles")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<PaginatedResponse<HelpArticleDTO>>>> GetAdminArticles(
        [FromQuery] string? category = null, [FromQuery] string? status = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        return HandleResponse(await _help.GetAdminArticlesAsync(RequireTenantId(_tenant), category, status, search, page, pageSize));
    }

    [HttpGet("admin/articles/{articleId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleDTO>>> GetAdminArticle(Guid articleId)
    {
        var voter = AdminRoleClaims.GetUserId(User)?.ToString();
        return HandleResponse(await _help.GetAdminArticleAsync(RequireTenantId(_tenant), articleId, voter));
    }

    [HttpPost("admin/articles")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleDTO>>> CreateArticle([FromBody] SaveHelpArticleDTO request) =>
        HandleResponse(await _help.CreateArticleAsync(RequireTenantId(_tenant), request));

    [HttpPut("admin/articles/{articleId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleDTO>>> UpdateArticle(
        Guid articleId, [FromBody] SaveHelpArticleDTO request) =>
        HandleResponse(await _help.UpdateArticleAsync(RequireTenantId(_tenant), articleId, request));

    [HttpPatch("admin/articles/{articleId:guid}/status")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleDTO>>> UpdateArticleStatus(
        Guid articleId, [FromBody] UpdateHelpArticleStatusDTO request) =>
        HandleResponse(await _help.UpdateArticleStatusAsync(RequireTenantId(_tenant), articleId, request.Status));

    [HttpDelete("admin/articles/{articleId:guid}")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<bool>>> DeleteArticle(Guid articleId) =>
        HandleResponse(await _help.DeleteArticleAsync(RequireTenantId(_tenant), articleId));

    [HttpPost("admin/upload-image")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<string>>> UploadImage(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(Response<string>.Fail("File is required"));
        await using var stream = file.OpenReadStream();
        return HandleResponse(await _help.UploadImageAsync(
            RequireTenantId(_tenant), AdminRoleClaims.GetUserId(User),
            file.FileName, file.ContentType, stream, file.Length));
    }

    [HttpPost("admin/articles/{articleId:guid}/comments")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleCommentDTO>>> AddComment(
        Guid articleId, [FromBody] AddHelpArticleCommentDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(Response<HelpArticleCommentDTO>.Fail("Body is required"));
        return HandleResponse(await _help.AddCommentAsync(
            RequireTenantId(_tenant), articleId, AdminRoleClaims.GetDisplayName(User), request.Body));
    }

    [HttpPost("admin/articles/{articleId:guid}/vote")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleEngagementDTO>>> Vote(
        Guid articleId, [FromBody] HelpArticleVoteRequestDTO request)
    {
        var voter = AdminRoleClaims.GetUserId(User)?.ToString() ?? "anonymous";
        return HandleResponse(await _help.VoteAsync(RequireTenantId(_tenant), articleId, voter, request.Vote));
    }

    [HttpGet("admin/articles/{articleId:guid}/engagement")]
    [Authorize(Policy = AdminAuthorizationPolicies.AdminSupport)]
    public async Task<ActionResult<Response<HelpArticleEngagementDTO>>> Engagement(Guid articleId)
    {
        var voter = AdminRoleClaims.GetUserId(User)?.ToString();
        return HandleResponse(await _help.GetEngagementAsync(RequireTenantId(_tenant), articleId, voter));
    }
}
