using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Services;

public interface IHelpService
{
    Task<Response<SupportTicketDTO>> SubmitTicketAsync(string tenantId, SubmitTicketDTO request);
    Task<Response<PaginatedResponse<SupportTicketDTO>>> GetAllTicketsAsync(string tenantId, string? status, int page, int pageSize);
    Task<Response<SupportTicketDTO>> GetTicketByIdAsync(string tenantId, Guid ticketId);
    Task<Response<SupportTicketDTO>> UpdateTicketStatusAsync(string tenantId, Guid ticketId, string status);
    Task<Response<SupportTicketDTO>> UpdateTicketAsync(string tenantId, Guid ticketId, UpdateTicketDTO request);
    Task<Response<SupportTicketReplyDTO>> AddTicketReplyAsync(string tenantId, Guid ticketId, Guid? senderId, string senderName, string message);
    Task<Response<SupportTicketDTO>> AssignTicketToMeAsync(string tenantId, Guid ticketId, Guid userId, string userName);
    Task<Response<SupportTicketDTO>> CreateTicketFromChatAsync(string tenantId, Guid sessionId, string? category, string? subject);
    Task<Response<TicketStatsDTO>> GetTicketStatsAsync(string tenantId);
    Task<Response<List<HelpArticleDTO>>> SuggestArticlesForTicketAsync(string tenantId, Guid ticketId, int limit = 5);

    Task<Response<PaginatedResponse<HelpArticleDTO>>> GetPublishedArticlesAsync(string tenantId, string? category, string? search, int page, int pageSize);
    Task<Response<HelpArticleDTO>> GetPublishedArticleAsync(string tenantId, Guid articleId);
    Task<Response<PaginatedResponse<HelpArticleDTO>>> GetAdminArticlesAsync(string tenantId, string? category, string? status, string? search, int page, int pageSize);
    Task<Response<HelpArticleDTO>> GetAdminArticleAsync(string tenantId, Guid articleId, string? voterKey);
    Task<Response<HelpArticleDTO>> CreateArticleAsync(string tenantId, SaveHelpArticleDTO request);
    Task<Response<HelpArticleDTO>> UpdateArticleAsync(string tenantId, Guid articleId, SaveHelpArticleDTO request);
    Task<Response<HelpArticleDTO>> UpdateArticleStatusAsync(string tenantId, Guid articleId, string status);
    Task<Response<bool>> DeleteArticleAsync(string tenantId, Guid articleId);
    Task<Response<string>> UploadImageAsync(string tenantId, Guid? createdBy, string fileName, string contentType, Stream content, long length);
    Task<Response<HelpArticleCommentDTO>> AddCommentAsync(string tenantId, Guid articleId, string authorName, string body);
    Task<Response<HelpArticleEngagementDTO>> VoteAsync(string tenantId, Guid articleId, string voterKey, string vote);
    Task<Response<HelpArticleEngagementDTO>> GetEngagementAsync(string tenantId, Guid articleId, string? voterKey);
}

public class HelpService : IHelpService
{
    private readonly ISupportStore _store;
    private readonly IProductRegistry _products;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HelpService> _logger;
    private readonly IAuditService _audit;
    private readonly IAppNotificationService _notify;

    public HelpService(
        ISupportStore store,
        IProductRegistry products,
        IConfiguration configuration,
        ILogger<HelpService> logger,
        IAuditService audit,
        IAppNotificationService notify)
    {
        _store = store;
        _products = products;
        _configuration = configuration;
        _logger = logger;
        _audit = audit;
        _notify = notify;
    }

    public async Task<Response<SupportTicketDTO>> SubmitTicketAsync(string tenantId, SubmitTicketDTO request)
    {
        try
        {
            var seq = await _store.NextTicketSequenceAsync(tenantId);
            var now = DateTime.UtcNow;
            var product = await _products.GetBySlugAsync(tenantId);
            var tier = product?.SupportTier ?? "standard";
            var frMinutes = TicketSla.FirstResponseMinutes(tier);
            var resolveMinutes = TicketSla.ResolveMinutes(tier);

            var ticket = new TicketEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketNumber = $"T-{now:yyyyMMdd}-{seq:D4}",
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim(),
                Subject = request.Subject.Trim(),
                Message = request.Message.Trim(),
                Priority = TicketPriority.FromCategory(request.Category),
                Status = TicketStatus.New,
                PageUrl = string.IsNullOrWhiteSpace(request.PageUrl) ? null : request.PageUrl.Trim(),
                AccountId = string.IsNullOrWhiteSpace(request.AccountId) ? null : request.AccountId.Trim(),
                SlaFirstResponseMinutes = frMinutes,
                FirstResponseDueAt = now.AddMinutes(frMinutes),
                ResolveDueAt = now.AddMinutes(resolveMinutes),
                CreatedAt = now,
                UpdatedAt = now
            };
            await _store.InsertTicketAsync(ticket);
            await AddEventAsync(tenantId, ticket.Id, "created", "Customer", $"Ticket opened ({tier} SLA)");
            return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, false), "Ticket submitted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitTicket failed");
            return Response<SupportTicketDTO>.Fail("Unable to submit ticket.");
        }
    }

    public async Task<Response<PaginatedResponse<SupportTicketDTO>>> GetAllTicketsAsync(
        string tenantId, string? status, int page, int pageSize)
    {
        try
        {
            var (items, total) = await _store.ListTicketsAsync(tenantId, status, page, pageSize);
            var dtos = new List<SupportTicketDTO>();
            foreach (var t in items)
                dtos.Add(await MapTicketAsync(tenantId, t, false));
            return Response<PaginatedResponse<SupportTicketDTO>>.SuccessResponse(
                PaginatedResponse<SupportTicketDTO>.Create(dtos, total, page, pageSize), "Tickets loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllTickets failed");
            return Response<PaginatedResponse<SupportTicketDTO>>.Fail("Unable to load tickets.");
        }
    }

    public async Task<Response<SupportTicketDTO>> GetTicketByIdAsync(string tenantId, Guid ticketId)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null) return Response<SupportTicketDTO>.Fail("Ticket not found");
        return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, true), "Ticket loaded");
    }

    public async Task<Response<SupportTicketDTO>> UpdateTicketStatusAsync(string tenantId, Guid ticketId, string status)
    {
        var normalized = TicketStatus.Normalize(status);
        if (!TicketStatus.All.Contains(normalized))
            return Response<SupportTicketDTO>.Fail("Invalid status");

        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null) return Response<SupportTicketDTO>.Fail("Ticket not found");
        var before = ticket.Status;
        ticket.Status = normalized;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateTicketAsync(ticket);
        await AddEventAsync(tenantId, ticket.Id, "status_changed", "Agent", $"Status → {normalized}");
        await _audit.WriteAsync(tenantId, AuditActions.TicketStatus, "ticket", ticket.Id.ToString(),
            null, "Agent", new { status = before }, new { status = normalized });
        return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, true), "Status updated");
    }

    public async Task<Response<SupportTicketDTO>> UpdateTicketAsync(string tenantId, Guid ticketId, UpdateTicketDTO request)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null) return Response<SupportTicketDTO>.Fail("Ticket not found");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var s = TicketStatus.Normalize(request.Status);
            if (!TicketStatus.All.Contains(s)) return Response<SupportTicketDTO>.Fail("Invalid status");
            var before = ticket.Status;
            ticket.Status = s;
            await _audit.WriteAsync(tenantId, AuditActions.TicketStatus, "ticket", ticket.Id.ToString(),
                null, "Agent", new { status = before }, new { status = s });
        }
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var p = request.Priority.Trim().ToLowerInvariant();
            if (!TicketPriority.All.Contains(p)) return Response<SupportTicketDTO>.Fail("Invalid priority");
            ticket.Priority = p;
        }
        if (request.Team != null)
            ticket.Team = string.IsNullOrWhiteSpace(request.Team) ? null : request.Team.Trim();
        if (request.AssignedToName != null)
        {
            ticket.AssignedToName = string.IsNullOrWhiteSpace(request.AssignedToName) ? null : request.AssignedToName.Trim();
            if (ticket.AssignedToName is null) ticket.AssignedTo = null;
        }
        if (request.AssignedTo.HasValue)
        {
            ticket.AssignedTo = request.AssignedTo.Value == Guid.Empty ? null : request.AssignedTo;
            if (ticket.AssignedTo is null && request.AssignedToName is null)
                ticket.AssignedToName = null;
        }
        if (request.Tags is not null)
            ticket.Tags = TicketTags.Serialize(request.Tags);
        if (request.EngTaskId.HasValue)
            ticket.EngTaskId = request.EngTaskId.Value == Guid.Empty ? null : request.EngTaskId;

        ticket.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateTicketAsync(ticket);
        await AddEventAsync(tenantId, ticket.Id, "updated", "Agent", "Ticket fields updated");
        return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, true), "Ticket updated");
    }

    public async Task<Response<SupportTicketReplyDTO>> AddTicketReplyAsync(
        string tenantId, Guid ticketId, Guid? senderId, string senderName, string message)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null) return Response<SupportTicketReplyDTO>.Fail("Ticket not found");

        var now = DateTime.UtcNow;
        var reply = new TicketReplyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            SenderType = TicketSenderType.Agent,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Support Agent" : senderName.Trim(),
            Message = message.Trim(),
            CreatedAt = now
        };
        await _store.InsertReplyAsync(reply);

        if (TicketStatus.IsNewOrOpen(ticket.Status))
            ticket.Status = TicketStatus.InProgress;
        if (ticket.FirstResponseAt is null)
            ticket.FirstResponseAt = now;
        ticket.UpdatedAt = now;
        await _store.UpdateTicketAsync(ticket);
        await AddEventAsync(tenantId, ticket.Id, "reply", senderName, "Agent reply added");

        return Response<SupportTicketReplyDTO>.SuccessResponse(new SupportTicketReplyDTO
        {
            Id = reply.Id,
            SenderType = reply.SenderType,
            SenderName = reply.SenderName,
            Message = reply.Message,
            CreatedAt = reply.CreatedAt
        }, "Reply added");
    }

    public async Task<Response<SupportTicketDTO>> AssignTicketToMeAsync(
        string tenantId, Guid ticketId, Guid userId, string userName)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null) return Response<SupportTicketDTO>.Fail("Ticket not found");
        ticket.AssignedTo = userId;
        ticket.AssignedToName = userName;
        if (TicketStatus.IsNewOrOpen(ticket.Status))
            ticket.Status = TicketStatus.InProgress;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateTicketAsync(ticket);
        await AddEventAsync(tenantId, ticket.Id, "assigned", userName, $"Assigned to {userName}");
        await _audit.WriteAsync(tenantId, AuditActions.TicketAssign, "ticket", ticket.Id.ToString(),
            userId, userName, null, new { assignedTo = userId, assignedToName = userName });
        await _notify.NotifyAsync(tenantId, "assign", $"Ticket assigned: {ticket.TicketNumber}",
            ticket.Subject, userId, userName, "ticket", ticket.Id.ToString());
        return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, true), "Assigned");
    }

    public async Task<Response<SupportTicketDTO>> CreateTicketFromChatAsync(
        string tenantId,
        Guid sessionId,
        string? category,
        string? subject)
    {
        var session = await _store.GetSessionAsync(tenantId, sessionId);
        if (session is null)
            return Response<SupportTicketDTO>.Fail("Chat session not found");

        var messages = await _store.ListMessagesAsync(tenantId, sessionId);
        var name = string.IsNullOrWhiteSpace(session.GuestName) ? "Chat visitor" : session.GuestName.Trim();
        var email = string.IsNullOrWhiteSpace(session.GuestEmail) ? "unknown@chat.local" : session.GuestEmail.Trim();
        return await CreateTicketFromChatCoreAsync(
            tenantId,
            sessionId,
            name,
            email,
            session.ExternalCustomerId,
            messages.Select(m => (m.SenderType, (string?)m.SenderName, m.Message)),
            category,
            subject);
    }

    /// <summary>
    /// Creates an ops-owned ticket from a bridged product-API chat session (no local session row required).
    /// </summary>
    public Task<Response<SupportTicketDTO>> CreateTicketFromChatAsync(
        string tenantId,
        Guid sessionId,
        ChatSessionDTO session,
        IReadOnlyList<ChatMessageDTO> messages,
        string? category,
        string? subject)
    {
        var name = string.IsNullOrWhiteSpace(session.CustomerName) ? "Chat visitor" : session.CustomerName.Trim();
        var email = string.IsNullOrWhiteSpace(session.CustomerEmail) ? "unknown@chat.local" : session.CustomerEmail.Trim();
        return CreateTicketFromChatCoreAsync(
            tenantId,
            sessionId,
            name,
            email,
            externalCustomerId: null,
            messages.Select(m => (m.SenderType, (string?)m.SenderName, m.Message)),
            category,
            subject);
    }

    private async Task<Response<SupportTicketDTO>> CreateTicketFromChatCoreAsync(
        string tenantId,
        Guid sessionId,
        string name,
        string email,
        Guid? externalCustomerId,
        IEnumerable<(string SenderType, string? SenderName, string Message)> messages,
        string? category,
        string? subject)
    {
        var transcript = string.Join("\n", messages.Select(m =>
            $"[{m.SenderType}] {m.SenderName ?? "Unknown"}: {m.Message}"));

        var cat = string.IsNullOrWhiteSpace(category) ? "chat" : category.Trim();
        var subj = string.IsNullOrWhiteSpace(subject)
            ? $"Chat conversation {sessionId.ToString()[..8]}"
            : subject.Trim();

        var seq = await _store.NextTicketSequenceAsync(tenantId);
        var now = DateTime.UtcNow;
        var product = await _products.GetBySlugAsync(tenantId);
        var tier = product?.SupportTier ?? "standard";
        var frMinutes = TicketSla.FirstResponseMinutes(tier);
        var resolveMinutes = TicketSla.ResolveMinutes(tier);
        var ticket = new TicketEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketNumber = $"T-{now:yyyyMMdd}-{seq:D4}",
            Name = name,
            Email = email,
            Category = cat,
            Subject = subj,
            Message = string.IsNullOrWhiteSpace(transcript)
                ? "Converted from live chat (no messages)."
                : transcript,
            Priority = TicketPriority.FromCategory(cat),
            Status = TicketStatus.New,
            ExternalCustomerId = externalCustomerId,
            ChatSessionId = sessionId,
            SlaFirstResponseMinutes = frMinutes,
            FirstResponseDueAt = now.AddMinutes(frMinutes),
            ResolveDueAt = now.AddMinutes(resolveMinutes),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertTicketAsync(ticket);
        await AddEventAsync(tenantId, ticket.Id, "created_from_chat", "Agent", $"Converted from chat {sessionId}");
        return Response<SupportTicketDTO>.SuccessResponse(await MapTicketAsync(tenantId, ticket, false), "Ticket created from chat");
    }

    public async Task<Response<List<HelpArticleDTO>>> SuggestArticlesForTicketAsync(
        string tenantId, Guid ticketId, int limit = 5)
    {
        var ticket = await _store.GetTicketAsync(tenantId, ticketId);
        if (ticket is null)
            return Response<List<HelpArticleDTO>>.Fail("Ticket not found");

        var category = ticket.Category;
        var (items, _) = await _store.ListArticlesAsync(
            tenantId, category, "published", null, 1, Math.Clamp(limit, 1, 20), true);

        if (items.Count == 0 && !string.IsNullOrWhiteSpace(ticket.Subject))
        {
            (items, _) = await _store.ListArticlesAsync(
                tenantId, null, "published", ticket.Subject, 1, Math.Clamp(limit, 1, 20), true);
        }

        var dtos = new List<HelpArticleDTO>();
        foreach (var a in items)
            dtos.Add(await MapArticleAsync(tenantId, a, false, null));

        return Response<List<HelpArticleDTO>>.SuccessResponse(dtos, "Suggestions loaded");
    }

    public async Task<Response<TicketStatsDTO>> GetTicketStatsAsync(string tenantId)
    {
        try
        {
            var stats = await _store.GetTicketStatsAsync(tenantId);
            return Response<TicketStatsDTO>.SuccessResponse(new TicketStatsDTO
            {
                OpenCount = stats.OpenCount,
                InProgressCount = stats.InProgressCount,
                WaitingCount = stats.WaitingCount,
                TotalCount = stats.TotalCount
            }, "Stats loaded");
        }
        catch (Exception ex)
        {
            return Response<TicketStatsDTO>.Fail($"Unable to load stats: {ex.Message}");
        }
    }

    public async Task<Response<PaginatedResponse<HelpArticleDTO>>> GetPublishedArticlesAsync(
        string tenantId, string? category, string? search, int page, int pageSize)
    {
        var (items, total) = await _store.ListArticlesAsync(tenantId, category, "published", search, page, pageSize, true);
        var dtos = new List<HelpArticleDTO>();
        foreach (var a in items)
            dtos.Add(await MapArticleAsync(tenantId, a, false, null));
        return Response<PaginatedResponse<HelpArticleDTO>>.SuccessResponse(
            PaginatedResponse<HelpArticleDTO>.Create(dtos, total, page, pageSize), "Articles loaded");
    }

    public async Task<Response<HelpArticleDTO>> GetPublishedArticleAsync(string tenantId, Guid articleId)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null || article.Status != HelpArticleStatus.Published)
            return Response<HelpArticleDTO>.Fail("Article not found");
        article.ViewCount++;
        article.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateArticleAsync(article);
        return Response<HelpArticleDTO>.SuccessResponse(await MapArticleAsync(tenantId, article, false, null), "Article loaded");
    }

    public async Task<Response<PaginatedResponse<HelpArticleDTO>>> GetAdminArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize)
    {
        var (items, total) = await _store.ListArticlesAsync(tenantId, category, status, search, page, pageSize, false);
        var dtos = new List<HelpArticleDTO>();
        foreach (var a in items)
            dtos.Add(await MapArticleAsync(tenantId, a, true, null));
        return Response<PaginatedResponse<HelpArticleDTO>>.SuccessResponse(
            PaginatedResponse<HelpArticleDTO>.Create(dtos, total, page, pageSize), "Articles loaded");
    }

    public async Task<Response<HelpArticleDTO>> GetAdminArticleAsync(string tenantId, Guid articleId, string? voterKey)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleDTO>.Fail("Article not found");
        return Response<HelpArticleDTO>.SuccessResponse(await MapArticleAsync(tenantId, article, true, voterKey), "Article loaded");
    }

    public async Task<Response<HelpArticleDTO>> CreateArticleAsync(string tenantId, SaveHelpArticleDTO request)
    {
        var now = DateTime.UtcNow;
        var status = HelpArticleStatus.Normalize(request.Status);
        var article = new KbArticleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Category = request.Category.Trim(),
            Content = request.Content ?? "",
            Status = status,
            HeroImageUrl = request.HeroImageUrl,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = status == HelpArticleStatus.Published ? now : null
        };
        await _store.InsertArticleAsync(article);
        await SaveStepsAsync(tenantId, article.Id, request.Steps);
        return Response<HelpArticleDTO>.SuccessResponse(await MapArticleAsync(tenantId, article, true, null), "Article created");
    }

    public async Task<Response<HelpArticleDTO>> UpdateArticleAsync(string tenantId, Guid articleId, SaveHelpArticleDTO request)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleDTO>.Fail("Article not found");

        var status = HelpArticleStatus.Normalize(request.Status);
        article.Title = request.Title.Trim();
        article.Category = request.Category.Trim();
        article.Content = request.Content ?? "";
        article.HeroImageUrl = request.HeroImageUrl;
        article.Status = status;
        article.UpdatedAt = DateTime.UtcNow;
        if (status == HelpArticleStatus.Published && article.PublishedAt is null)
            article.PublishedAt = DateTime.UtcNow;
        await _store.UpdateArticleAsync(article);
        await SaveStepsAsync(tenantId, article.Id, request.Steps);
        return Response<HelpArticleDTO>.SuccessResponse(await MapArticleAsync(tenantId, article, true, null), "Article updated");
    }

    public async Task<Response<HelpArticleDTO>> UpdateArticleStatusAsync(string tenantId, Guid articleId, string status)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleDTO>.Fail("Article not found");
        article.Status = HelpArticleStatus.Normalize(status);
        article.UpdatedAt = DateTime.UtcNow;
        if (article.Status == HelpArticleStatus.Published && article.PublishedAt is null)
            article.PublishedAt = DateTime.UtcNow;
        await _store.UpdateArticleAsync(article);
        return Response<HelpArticleDTO>.SuccessResponse(await MapArticleAsync(tenantId, article, true, null), "Status updated");
    }

    public async Task<Response<bool>> DeleteArticleAsync(string tenantId, Guid articleId)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<bool>.Fail("Article not found");
        await _store.DeleteArticleAsync(tenantId, articleId);
        return Response<bool>.SuccessResponse(true, "Article deleted");
    }

    public async Task<Response<string>> UploadImageAsync(
        string tenantId, Guid? createdBy, string fileName, string contentType, Stream content, long length)
    {
        if (length > 5 * 1024 * 1024)
            return Response<string>.Fail("Image must be 5MB or less.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var path = $"help/{tenantId}/{Guid.NewGuid():N}{ext}";

        // Storage upload is best-effort; metadata always recorded. Public URL is constructed from config.
        var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/') ?? "";
        var publicUrl = $"{supabaseUrl}/storage/v1/object/public/app-images/{path}";

        var upload = new UploadEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Path = path,
            PublicUrl = publicUrl,
            ContentType = contentType,
            SizeBytes = length,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertUploadAsync(upload);
        return Response<string>.SuccessResponse(publicUrl, "Uploaded");
    }

    public async Task<Response<HelpArticleCommentDTO>> AddCommentAsync(
        string tenantId, Guid articleId, string authorName, string body)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleCommentDTO>.Fail("Article not found");

        var comment = new KbCommentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArticleId = articleId,
            AuthorName = authorName,
            Body = body.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertCommentAsync(comment);
        return Response<HelpArticleCommentDTO>.SuccessResponse(new HelpArticleCommentDTO
        {
            Id = comment.Id,
            AuthorName = comment.AuthorName,
            Body = comment.Body,
            CreatedAt = comment.CreatedAt
        }, "Comment added");
    }

    public async Task<Response<HelpArticleEngagementDTO>> VoteAsync(
        string tenantId, Guid articleId, string voterKey, string vote)
    {
        var normalized = HelpArticleVoteValue.Normalize(vote);
        if (normalized is null) return Response<HelpArticleEngagementDTO>.Fail("Invalid vote");
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleEngagementDTO>.Fail("Article not found");

        await _store.UpsertVoteAsync(new KbVoteEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArticleId = articleId,
            VoterKey = voterKey,
            Vote = normalized,
            CreatedAt = DateTime.UtcNow
        });
        return await GetEngagementAsync(tenantId, articleId, voterKey);
    }

    public async Task<Response<HelpArticleEngagementDTO>> GetEngagementAsync(
        string tenantId, Guid articleId, string? voterKey)
    {
        var article = await _store.GetArticleAsync(tenantId, articleId);
        if (article is null) return Response<HelpArticleEngagementDTO>.Fail("Article not found");

        var votes = await _store.ListVotesAsync(tenantId, articleId);
        var comments = await _store.ListCommentsAsync(tenantId, articleId);
        return Response<HelpArticleEngagementDTO>.SuccessResponse(new HelpArticleEngagementDTO
        {
            LikeCount = votes.Count(v => v.Vote == HelpArticleVoteValue.Like),
            DislikeCount = votes.Count(v => v.Vote == HelpArticleVoteValue.Dislike),
            MyVote = voterKey is null ? null : votes.FirstOrDefault(v => v.VoterKey == voterKey)?.Vote,
            Comments = comments.Select(c => new HelpArticleCommentDTO
            {
                Id = c.Id,
                AuthorName = c.AuthorName,
                Body = c.Body,
                CreatedAt = c.CreatedAt
            }).ToList()
        }, "Engagement loaded");
    }

    private async Task SaveStepsAsync(string tenantId, Guid articleId, List<HelpArticleStepDTO> steps)
    {
        var entities = (steps ?? [])
            .Select((s, i) => new KbStepEntity
            {
                Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
                TenantId = tenantId,
                ArticleId = articleId,
                SortOrder = s.SortOrder != 0 ? s.SortOrder : i,
                Detail = s.Detail ?? "",
                ImageUrl = s.ImageUrl
            }).ToList();
        await _store.ReplaceStepsAsync(tenantId, articleId, entities);
    }

    private async Task AddEventAsync(string tenantId, Guid ticketId, string eventType, string? actor, string? detail)
    {
        await _store.InsertTicketEventAsync(new TicketEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            EventType = eventType,
            ActorName = actor,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<SupportTicketDTO> MapTicketAsync(string tenantId, TicketEntity t, bool includeReplies)
    {
        var replies = await _store.ListRepliesAsync(tenantId, t.Id);

        return new SupportTicketDTO
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber,
            Name = t.Name,
            Email = t.Email,
            Category = t.Category,
            Subject = t.Subject,
            Message = t.Message,
            Priority = t.Priority,
            Status = t.Status,
            Team = t.Team,
            AssignedTo = t.AssignedTo,
            AssignedToName = t.AssignedToName,
            FirstResponseAt = t.FirstResponseAt,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            ReplyCount = replies.Count,
            PageUrl = t.PageUrl,
            AccountId = t.AccountId,
            ChatSessionId = t.ChatSessionId,
            Tags = TicketTags.Parse(t.Tags),
            SlaFirstResponseMinutes = t.SlaFirstResponseMinutes,
            FirstResponseDueAt = t.FirstResponseDueAt,
            ResolveDueAt = t.ResolveDueAt,
            EngTaskId = t.EngTaskId,
            Timeline = includeReplies
                ? (await _store.ListTicketEventsAsync(tenantId, t.Id)).Select(e => new TicketEventDTO
                {
                    Id = e.Id,
                    EventType = e.EventType,
                    ActorName = e.ActorName,
                    Detail = e.Detail,
                    CreatedAt = e.CreatedAt
                }).ToList()
                : [],
            Replies = includeReplies
                ? replies.Select(r => new SupportTicketReplyDTO
                {
                    Id = r.Id,
                    SenderType = r.SenderType,
                    SenderName = r.SenderName,
                    Message = r.Message,
                    CreatedAt = r.CreatedAt
                }).ToList()
                : []
        };
    }

    private async Task<HelpArticleDTO> MapArticleAsync(
        string tenantId, KbArticleEntity a, bool includeAdmin, string? voterKey)
    {
        var steps = await _store.ListStepsAsync(tenantId, a.Id);
        var dto = new HelpArticleDTO
        {
            Id = a.Id,
            Title = a.Title,
            Category = a.Category,
            Content = a.Content,
            Status = a.Status,
            HeroImageUrl = a.HeroImageUrl,
            ViewCount = a.ViewCount,
            HelpfulCount = a.HelpfulCount,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            PublishedAt = a.PublishedAt,
            Steps = steps.Select(s => new HelpArticleStepDTO
            {
                Id = s.Id,
                SortOrder = s.SortOrder,
                Detail = s.Detail,
                ImageUrl = s.ImageUrl
            }).ToList()
        };

        if (includeAdmin)
        {
            var votes = await _store.ListVotesAsync(tenantId, a.Id);
            var comments = await _store.ListCommentsAsync(tenantId, a.Id);
            dto.LikeCount = votes.Count(v => v.Vote == HelpArticleVoteValue.Like);
            dto.DislikeCount = votes.Count(v => v.Vote == HelpArticleVoteValue.Dislike);
            dto.MyVote = voterKey is null ? null : votes.FirstOrDefault(v => v.VoterKey == voterKey)?.Vote;
            dto.Comments = comments.Select(c => new HelpArticleCommentDTO
            {
                Id = c.Id,
                AuthorName = c.AuthorName,
                Body = c.Body,
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        return dto;
    }
}
