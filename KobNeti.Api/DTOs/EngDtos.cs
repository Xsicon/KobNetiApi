namespace KobNeti.Api.DTOs;

public class EngTaskDTO
{
    public Guid Id { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = EngTaskType.Feature;
    public string Status { get; set; } = EngTaskStatus.Backlog;
    public string Priority { get; set; } = EngTaskPriority.Medium;
    public decimal? EstimatePoints { get; set; }
    public string? AssigneeName { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string? GithubPrUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateEngTaskDTO
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = EngTaskType.Feature;
    public string Status { get; set; } = EngTaskStatus.Backlog;
    public string Priority { get; set; } = EngTaskPriority.Medium;
    public decimal? EstimatePoints { get; set; }
    public string? AssigneeName { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string? GithubPrUrl { get; set; }
}

public class UpdateEngTaskDTO
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? TaskType { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public decimal? EstimatePoints { get; set; }
    public string? AssigneeName { get; set; }
    public Guid? TicketId { get; set; }
    public bool ClearTicketId { get; set; }
    public Guid? MilestoneId { get; set; }
    public bool ClearMilestoneId { get; set; }
    public string? GithubPrUrl { get; set; }
    public bool ClearGithubPrUrl { get; set; }
}

public class MilestoneDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = MilestoneStatus.Planned;
    public DateOnly? TargetDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public int SortOrder { get; set; }
    public Guid? CalendarEventId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int TaskCount { get; set; }
}

public class CreateMilestoneDTO
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = MilestoneStatus.Planned;
    public DateOnly? TargetDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateMilestoneDTO
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool ClearTargetDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public bool ClearStartDate { get; set; }
    public int? SortOrder { get; set; }
}

public class CalendarEventDTO
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = "milestone";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
}

public class GithubCacheDTO
{
    public string? RepoUrl { get; set; }
    public DateTime? PullsFetchedAt { get; set; }
    public DateTime? CommitsFetchedAt { get; set; }
    public List<GithubPullDTO> Pulls { get; set; } = [];
    public List<GithubCommitDTO> Commits { get; set; } = [];
    public string? Message { get; set; }
}

public class GithubPullDTO
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GithubCommitDTO
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTime? Date { get; set; }
}

public class UpdateProductGithubRepoDTO
{
    public string? GithubRepoUrl { get; set; }
}

public static class EngTaskType
{
    public const string Feature = "feature";
    public const string Bug = "bug";
    public const string Chore = "chore";
    public const string Spike = "spike";
    public const string Incident = "incident";
    public static readonly string[] All = [Feature, Bug, Chore, Spike, Incident];
}

public static class EngTaskStatus
{
    public const string Backlog = "backlog";
    public const string Ready = "ready";
    public const string InProgress = "in_progress";
    public const string InReview = "in_review";
    public const string Done = "done";
    public const string Cancelled = "cancelled";
    public static readonly string[] All = [Backlog, Ready, InProgress, InReview, Done, Cancelled];
    public static readonly string[] BoardOrder = [Backlog, Ready, InProgress, InReview, Done];
}

public static class EngTaskPriority
{
    public const string Critical = "critical";
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
    public static readonly string[] All = [Critical, High, Medium, Low];
}

public static class MilestoneStatus
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public static readonly string[] All = [Planned, Active, Completed, Cancelled];
}
