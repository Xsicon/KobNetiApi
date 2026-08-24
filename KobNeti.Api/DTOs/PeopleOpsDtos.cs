namespace KobNeti.Api.DTOs;

public class TimeEntryDTO
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EntryType { get; set; } = TimeEntryType.Manual;
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public int? Minutes { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? EngTaskId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = TimeEntryStatus.Approved;
    public Guid? SupersedesId { get; set; }
    public Guid? ApprovalId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClockInDTO
{
    public Guid? TicketId { get; set; }
    public Guid? EngTaskId { get; set; }
    public string? Notes { get; set; }
}

public class ManualTimeDTO
{
    public int Minutes { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? EngTaskId { get; set; }
    public string? Notes { get; set; }
    public DateTime? WorkedAt { get; set; }
}

public class RequestTimeEditDTO
{
    public Guid EntryId { get; set; }
    public int? Minutes { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
}

public class ApprovalRequestDTO
{
    public Guid Id { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = ApprovalStatus.Pending;
    public string PayloadJson { get; set; } = "{}";
    public Guid? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DecideApprovalDTO
{
    public bool Approve { get; set; }
}

public class PayRateDTO
{
    public Guid Id { get; set; }
    public Guid? StaffId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SavePayRateDTO
{
    public Guid? StaffId { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "USD";
    public DateOnly? EffectiveFrom { get; set; }
}

public class PayPeriodDTO
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string Status { get; set; } = PayPeriodStatus.Open;
    public int TotalMinutes { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public List<PayPeriodLineDTO> Lines { get; set; } = [];
    public Guid? ApprovalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PayPeriodLineDTO
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal Amount { get; set; }
}

public class CreatePayPeriodDTO
{
    public string Label { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
}

public static class TimeEntryType
{
    public const string Clock = "clock";
    public const string Manual = "manual";
}

public static class TimeEntryStatus
{
    public const string Open = "open";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Superseded = "superseded";
}

public static class ApprovalStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class ApprovalRequestType
{
    public const string TimeEdit = "time_edit";
    public const string Payroll = "payroll";
}

public static class PayPeriodStatus
{
    public const string Open = "open";
    public const string Calculated = "calculated";
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Exported = "exported";
}
