namespace KobNeti.Api.DTOs;

public class SendMessageRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
}

public class ChatMessageDTO
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string SenderType { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class ChatSessionDTO
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public string? LastMessagePreview { get; set; }
    public string? LastMessageSender { get; set; }
    public int MessageCount { get; set; }
    public int UnreadMessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatStickyNoteDTO
{
    public Guid SessionId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string ReasonForContact { get; set; } = string.Empty;
    public List<string> KeyActionsTaken { get; set; } = [];
    public string ColorHex { get; set; } = "#F29D68";
    public bool Pinned { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveChatStickyNoteDTO
{
    public string? AgentName { get; set; }
    public string? ReasonForContact { get; set; }
    public List<string>? KeyActionsTaken { get; set; }
    public string? ColorHex { get; set; }
    public bool Pinned { get; set; }
}
