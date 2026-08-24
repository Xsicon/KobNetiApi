namespace KobNeti.Api.DTOs;

public class IncidentDTO
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = IncidentSeverity.Sev3;
    public string Status { get; set; } = IncidentStatus.Open;
    public string? CommanderName { get; set; }
    public Guid? CommanderUserId { get; set; }
    public Guid? SourceTicketId { get; set; }
    public string? PostmortemNotes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<IncidentEventDTO> Timeline { get; set; } = [];
}

public class IncidentEventDTO
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string? Detail { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CreateIncidentDTO
{
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = IncidentSeverity.Sev3;
    public string? CommanderName { get; set; }
    public Guid? SourceTicketId { get; set; }
}

public class EscalateTicketDTO
{
    public string? Title { get; set; }
    public string Severity { get; set; } = IncidentSeverity.Sev2;
    public string? CommanderName { get; set; }
}

public class UpdateIncidentDTO
{
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? CommanderName { get; set; }
    public string? PostmortemNotes { get; set; }
}

public static class IncidentSeverity
{
    public const string Sev1 = "sev1";
    public const string Sev2 = "sev2";
    public const string Sev3 = "sev3";
    public const string Sev4 = "sev4";
    public static readonly string[] All = [Sev1, Sev2, Sev3, Sev4];
}

public static class IncidentStatus
{
    public const string Open = "open";
    public const string Investigating = "investigating";
    public const string Mitigated = "mitigated";
    public const string Resolved = "resolved";
    public const string Closed = "closed";
    public static readonly string[] All = [Open, Investigating, Mitigated, Resolved, Closed];
}
