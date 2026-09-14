namespace KobNeti.Api.DTOs;

public class AuditEventDTO
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationDTO
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Channel { get; set; } = "in_app";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
}

public class NotificationPrefsDTO
{
    public Guid? UserId { get; set; }
    public bool AssignEnabled { get; set; } = true;
    public bool ApprovalEnabled { get; set; } = true;
    public bool EscalationEnabled { get; set; } = true;
    public bool ReminderEnabled { get; set; } = true;
}

public class CreateCalendarEventDTO
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "meeting";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

public class OpsFileDTO
{
    public Guid Id { get; set; }
    public string FolderPath { get; set; } = "/";
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateOpsFileDTO
{
    public string FolderPath { get; set; } = "/";
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? PublicUrl { get; set; }
}

public class IntegrationDTO
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "disconnected";
    public bool HasSecrets { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
}

public class ConnectIntegrationDTO
{
    public string Provider { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Dictionary<string, string>? Secrets { get; set; }
}

public static class AuditActions
{
    public const string AuthExchange = "auth.exchange";
    public const string TicketStatus = "ticket.status";
    public const string TicketAssign = "ticket.assign";
    public const string PayrollCalculate = "payroll.calculate";
    public const string PayrollSubmit = "payroll.submit";
    public const string PayrollExport = "payroll.export";
    public const string PayrollRate = "payroll.rate";
    public const string ApprovalDecide = "approval.decide";
    public const string StaffInvite = "staff.invite";
    public const string StaffProducts = "staff.products";
    public const string StaffActivate = "staff.activate";
    public const string StaffStatus = "staff.status";
    public const string IncidentEscalate = "incident.escalate";
}
