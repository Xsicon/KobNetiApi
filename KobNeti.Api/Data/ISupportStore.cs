namespace KobNeti.Api.Data;

/// <summary>
/// Tenant-scoped persistence. Every method requires tenantId and must filter by it.
/// </summary>
public interface ISupportStore
{
    // Chat
    Task<ChatSessionEntity?> GetSessionAsync(string tenantId, Guid sessionId);
    Task<ChatSessionEntity> InsertSessionAsync(ChatSessionEntity session);
    Task UpdateSessionAsync(ChatSessionEntity session);
    Task<List<ChatSessionEntity>> ListActiveSessionsAsync(string tenantId);
    Task<ChatMessageEntity> InsertMessageAsync(ChatMessageEntity message);
    Task<List<ChatMessageEntity>> ListMessagesAsync(string tenantId, Guid sessionId);
    Task<List<ChatMessageEntity>> ListMessagesForSessionsAsync(string tenantId, IEnumerable<Guid> sessionIds);

    // Tickets
    Task<TicketEntity> InsertTicketAsync(TicketEntity ticket);
    Task<TicketEntity?> GetTicketAsync(string tenantId, Guid ticketId);
    Task UpdateTicketAsync(TicketEntity ticket);
    Task<(List<TicketEntity> Items, int Total)> ListTicketsAsync(string tenantId, string? status, int page, int pageSize);
    Task<TicketReplyEntity> InsertReplyAsync(TicketReplyEntity reply);
    Task<List<TicketReplyEntity>> ListRepliesAsync(string tenantId, Guid ticketId);
    Task<TicketEventEntity> InsertTicketEventAsync(TicketEventEntity evt);
    Task<List<TicketEventEntity>> ListTicketEventsAsync(string tenantId, Guid ticketId);
    Task<TicketStatsSnapshot> GetTicketStatsAsync(string tenantId);
    Task<int> CountOpenTicketsAsync(string tenantId);
    Task<int> NextTicketSequenceAsync(string tenantId);

    // KB
    Task<KbArticleEntity> InsertArticleAsync(KbArticleEntity article);
    Task ReplaceStepsAsync(string tenantId, Guid articleId, List<KbStepEntity> steps);
    Task<KbArticleEntity?> GetArticleAsync(string tenantId, Guid articleId);
    Task UpdateArticleAsync(KbArticleEntity article);
    Task DeleteArticleAsync(string tenantId, Guid articleId);
    Task<(List<KbArticleEntity> Items, int Total)> ListArticlesAsync(
        string tenantId, string? category, string? status, string? search, int page, int pageSize, bool publishedOnly);
    Task<List<KbStepEntity>> ListStepsAsync(string tenantId, Guid articleId);
    Task<KbCommentEntity> InsertCommentAsync(KbCommentEntity comment);
    Task<List<KbCommentEntity>> ListCommentsAsync(string tenantId, Guid articleId);
    Task UpsertVoteAsync(KbVoteEntity vote);
    Task<List<KbVoteEntity>> ListVotesAsync(string tenantId, Guid articleId);
    Task<UploadEntity> InsertUploadAsync(UploadEntity upload);

    // Macros
    Task<List<MacroEntity>> ListMacrosAsync(string tenantId);
    Task<MacroEntity?> GetMacroAsync(string tenantId, Guid id);
    Task<MacroEntity> InsertMacroAsync(MacroEntity macro);
    Task UpdateMacroAsync(MacroEntity macro);
    Task DeleteMacroAsync(string tenantId, Guid id);

    // Incidents
    Task<IncidentEntity> InsertIncidentAsync(IncidentEntity incident);
    Task<IncidentEntity?> GetIncidentAsync(string tenantId, Guid incidentId);
    Task UpdateIncidentAsync(IncidentEntity incident);
    Task<(List<IncidentEntity> Items, int Total)> ListIncidentsAsync(string tenantId, string? status, int page, int pageSize);
    Task<int> NextIncidentSequenceAsync(string tenantId);
    Task<IncidentEventEntity> InsertIncidentEventAsync(IncidentEventEntity evt);
    Task<List<IncidentEventEntity>> ListIncidentEventsAsync(string tenantId, Guid incidentId);

    // Engineering tasks
    Task<EngTaskEntity> InsertEngTaskAsync(EngTaskEntity task);
    Task<EngTaskEntity?> GetEngTaskAsync(string tenantId, Guid taskId);
    Task UpdateEngTaskAsync(EngTaskEntity task);
    Task<(List<EngTaskEntity> Items, int Total)> ListEngTasksAsync(
        string tenantId, string? status, Guid? milestoneId, int page, int pageSize);
    Task<int> NextEngTaskSequenceAsync(string tenantId);

    // Milestones
    Task<EngMilestoneEntity> InsertMilestoneAsync(EngMilestoneEntity milestone);
    Task<EngMilestoneEntity?> GetMilestoneAsync(string tenantId, Guid milestoneId);
    Task UpdateMilestoneAsync(EngMilestoneEntity milestone);
    Task<List<EngMilestoneEntity>> ListMilestonesAsync(string tenantId);

    // Calendar events (W4.7 / W6.7)
    Task<CalendarEventEntity> UpsertCalendarEventAsync(CalendarEventEntity evt);
    Task<CalendarEventEntity?> GetCalendarEventAsync(string tenantId, Guid eventId);
    Task DeleteCalendarEventAsync(string tenantId, Guid eventId);
    Task<List<CalendarEventEntity>> ListCalendarEventsAsync(string tenantId);

    // GitHub read-only cache
    Task UpsertGithubCacheAsync(GithubCacheEntity cache);
    Task<GithubCacheEntity?> GetGithubCacheAsync(string tenantId, string cacheKind);

    // Time entries (W5)
    Task<TimeEntryEntity> InsertTimeEntryAsync(TimeEntryEntity entry);
    Task<TimeEntryEntity?> GetTimeEntryAsync(string tenantId, Guid id);
    Task UpdateTimeEntryAsync(TimeEntryEntity entry);
    Task<List<TimeEntryEntity>> ListTimeEntriesAsync(string tenantId, Guid? userId, string? status);
    Task<TimeEntryEntity?> GetOpenClockAsync(string tenantId, Guid? userId);

    // Approvals
    Task<ApprovalRequestEntity> InsertApprovalAsync(ApprovalRequestEntity request);
    Task<ApprovalRequestEntity?> GetApprovalAsync(string tenantId, Guid id);
    Task UpdateApprovalAsync(ApprovalRequestEntity request);
    Task<List<ApprovalRequestEntity>> ListApprovalsAsync(string tenantId, string? status);

    // Pay rates / periods
    Task<PayRateEntity> InsertPayRateAsync(PayRateEntity rate);
    Task<List<PayRateEntity>> ListPayRatesAsync(string tenantId);
    Task<PayPeriodEntity> InsertPayPeriodAsync(PayPeriodEntity period);
    Task<PayPeriodEntity?> GetPayPeriodAsync(string tenantId, Guid id);
    Task UpdatePayPeriodAsync(PayPeriodEntity period);
    Task<List<PayPeriodEntity>> ListPayPeriodsAsync(string tenantId);

    // W6 platform glue
    Task InsertAuditEventAsync(AuditEventEntity evt);
    Task<List<AuditEventEntity>> ListAuditEventsAsync(
        string tenantId, string? action, string? entityType, string? search, int take);

    Task InsertNotificationAsync(NotificationEntity n);
    Task<NotificationEntity?> GetNotificationAsync(string tenantId, Guid id);
    Task UpdateNotificationAsync(NotificationEntity n);
    Task<List<NotificationEntity>> ListNotificationsAsync(string tenantId, Guid? userId, bool unreadOnly);
    Task<NotificationPrefsEntity?> GetNotificationPrefsAsync(string tenantId, Guid userId);
    Task UpsertNotificationPrefsAsync(NotificationPrefsEntity prefs);

    Task InsertOpsFileAsync(OpsFileEntity file);
    Task DeleteOpsFileAsync(string tenantId, Guid id);
    Task<List<OpsFileEntity>> ListOpsFilesAsync(string tenantId, string? folderPath);

    Task<IntegrationEntity> UpsertIntegrationAsync(IntegrationEntity integration);
    Task<IntegrationEntity?> GetIntegrationAsync(string tenantId, string provider);
    Task<List<IntegrationEntity>> ListIntegrationsAsync(string tenantId);
    Task UpsertIntegrationSecretAsync(IntegrationSecretEntity secret);
    Task DeleteIntegrationSecretsAsync(string tenantId, string provider);
    Task<List<IntegrationSecretEntity>> ListIntegrationSecretsAsync(string tenantId, string provider);

    // W7
    Task<List<PlatformHelpArticleEntity>> ListPlatformHelpAsync(bool publishedOnly);
    Task<PlatformHelpArticleEntity?> GetPlatformHelpBySlugAsync(string slug);
    Task<PlatformHelpArticleEntity> UpsertPlatformHelpAsync(PlatformHelpArticleEntity article);

    Task InsertReportRunAsync(ReportRunEntity run);
    Task<ReportRunEntity?> GetReportRunAsync(string tenantId, Guid id);
    Task<List<ReportRunEntity>> ListReportRunsAsync(string tenantId);

    Task<ImChannelEntity> InsertImChannelAsync(ImChannelEntity channel);
    Task<ImChannelEntity?> GetImChannelAsync(string tenantId, Guid id);
    Task<List<ImChannelEntity>> ListImChannelsAsync(string tenantId);
    Task InsertImMessageAsync(ImMessageEntity message);
    Task<List<ImMessageEntity>> ListImMessagesAsync(string tenantId, Guid channelId);

    Task InsertAssetAsync(AssetEntity asset);
    Task UpdateAssetAsync(AssetEntity asset);
    Task<AssetEntity?> GetAssetAsync(string tenantId, Guid id);
    Task<List<AssetEntity>> ListAssetsAsync(string tenantId);
    Task<List<AssetEntity>> ListAssetsRenewingSoonAsync(string tenantId, DateOnly before);

    Task<int> CountActiveChatsAsync(string tenantId);
}

public record TicketStatsSnapshot(int OpenCount, int InProgressCount, int TotalCount, int WaitingCount = 0);
