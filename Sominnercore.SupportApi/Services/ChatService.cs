using Sominnercore.SupportApi.Data;
using Sominnercore.SupportApi.DTOs;
using Sominnercore.SupportApi.Shared;

namespace Sominnercore.SupportApi.Services;

public interface IChatService
{
    Task<Response<ChatMessageDTO>> SendMessageAsync(string tenantId, SendMessageRequest request, Guid? userId, bool isAdmin);
    Task<Response<List<ChatMessageDTO>>> GetMessagesAsync(string tenantId, Guid sessionId, Guid? userId, bool isAdmin);
    Task<Response<List<ChatSessionDTO>>> GetActiveSessionsAsync(string tenantId);
    Task<Response<bool>> CloseSessionAsync(string tenantId, Guid sessionId);
    Task<Response<string>> GetSessionStatusAsync(string tenantId, Guid sessionId, Guid? userId, bool isAdmin);
    Task<Response<ChatSessionDTO>> GetSessionAsync(string tenantId, Guid sessionId);
}

public class ChatService : IChatService
{
    private readonly ISupportStore _store;
    private readonly ILogger<ChatService> _logger;

    public ChatService(ISupportStore store, ILogger<ChatService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<Response<ChatMessageDTO>> SendMessageAsync(
        string tenantId, SendMessageRequest request, Guid? userId, bool isAdmin)
    {
        try
        {
            Guid sessionId;
            if (request.SessionId.HasValue)
            {
                sessionId = request.SessionId.Value;
                var existing = await _store.GetSessionAsync(tenantId, sessionId);
                if (existing is null)
                    return Response<ChatMessageDTO>.Fail("Session not found");

                if (!isAdmin && !CanAccessSession(existing, userId))
                    return Response<ChatMessageDTO>.Fail("Forbidden");
            }
            else
            {
                if (isAdmin)
                    return Response<ChatMessageDTO>.Fail("Admins cannot create new sessions");

                var newSession = new ChatSessionEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ExternalCustomerId = userId,
                    GuestName = request.GuestName,
                    GuestEmail = request.GuestEmail,
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _store.InsertSessionAsync(newSession);
                sessionId = newSession.Id;
            }

            var senderType = isAdmin ? "admin" : "customer";
            var senderName = isAdmin ? "Support Team" : (request.GuestName ?? "Customer");
            var message = new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = sessionId,
                SenderType = senderType,
                SenderId = userId,
                SenderName = senderName,
                Message = request.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            var saved = await _store.InsertMessageAsync(message);

            var session = await _store.GetSessionAsync(tenantId, sessionId);
            if (session is not null)
            {
                session.UpdatedAt = DateTime.UtcNow;
                await _store.UpdateSessionAsync(session);
            }

            return Response<ChatMessageDTO>.SuccessResponse(ToMessageDto(saved), "Message sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessage failed");
            return Response<ChatMessageDTO>.Fail("Unable to send message.");
        }
    }

    public async Task<Response<List<ChatMessageDTO>>> GetMessagesAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        try
        {
            var session = await _store.GetSessionAsync(tenantId, sessionId);
            if (session is null)
                return Response<List<ChatMessageDTO>>.Fail("Session not found");

            if (!isAdmin && !CanAccessSession(session, userId))
                return Response<List<ChatMessageDTO>>.Fail("Forbidden");

            var messages = await _store.ListMessagesAsync(tenantId, sessionId);
            return Response<List<ChatMessageDTO>>.SuccessResponse(
                messages.Select(ToMessageDto).ToList(), "Messages loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMessages failed");
            return Response<List<ChatMessageDTO>>.Fail("Unable to load messages.");
        }
    }

    public async Task<Response<List<ChatSessionDTO>>> GetActiveSessionsAsync(string tenantId)
    {
        try
        {
            var sessions = await _store.ListActiveSessionsAsync(tenantId);
            var allMsgs = await _store.ListMessagesForSessionsAsync(tenantId, sessions.Select(s => s.Id));
            var bySession = allMsgs.GroupBy(m => m.SessionId).ToDictionary(g => g.Key, g => g.ToList());

            var dtos = sessions.Select(s =>
            {
                bySession.TryGetValue(s.Id, out var msgs);
                msgs ??= [];
                var last = msgs.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
                return new ChatSessionDTO
                {
                    Id = s.Id,
                    CustomerName = s.GuestName ?? "Guest",
                    CustomerEmail = s.GuestEmail,
                    Status = s.Status,
                    LastActivity = s.UpdatedAt,
                    LastMessagePreview = Truncate(last?.Message),
                    LastMessageSender = last?.SenderType,
                    MessageCount = msgs.Count,
                    UnreadMessageCount = msgs.Count(m => !m.IsRead && m.SenderType == "customer"),
                    CreatedAt = s.CreatedAt
                };
            }).ToList();

            return Response<List<ChatSessionDTO>>.SuccessResponse(dtos, "Active sessions loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActiveSessions failed");
            return Response<List<ChatSessionDTO>>.Fail("Unable to load active sessions.");
        }
    }

    public async Task<Response<bool>> CloseSessionAsync(string tenantId, Guid sessionId)
    {
        try
        {
            var session = await _store.GetSessionAsync(tenantId, sessionId);
            if (session is null)
                return Response<bool>.Fail("Session not found");

            session.Status = "closed";
            session.ClosedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;
            await _store.UpdateSessionAsync(session);
            return Response<bool>.SuccessResponse(true, "Session closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloseSession failed");
            return Response<bool>.Fail("Unable to close session.");
        }
    }

    public async Task<Response<string>> GetSessionStatusAsync(
        string tenantId, Guid sessionId, Guid? userId, bool isAdmin)
    {
        try
        {
            var session = await _store.GetSessionAsync(tenantId, sessionId);
            if (session is null)
                return Response<string>.Fail("Session not found");

            if (!isAdmin && !CanAccessSession(session, userId))
                return Response<string>.Fail("Forbidden");

            return Response<string>.SuccessResponse(session.Status, "Status fetched");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSessionStatus failed");
            return Response<string>.Fail("Unable to load session status.");
        }
    }

    public async Task<Response<ChatSessionDTO>> GetSessionAsync(string tenantId, Guid sessionId)
    {
        try
        {
            var session = await _store.GetSessionAsync(tenantId, sessionId);
            if (session is null)
                return Response<ChatSessionDTO>.Fail("Session not found");

            var msgs = await _store.ListMessagesAsync(tenantId, sessionId);
            var last = msgs.LastOrDefault();
            var dto = new ChatSessionDTO
            {
                Id = session.Id,
                CustomerName = session.GuestName ?? "Guest",
                CustomerEmail = session.GuestEmail,
                Status = session.Status,
                LastActivity = session.UpdatedAt,
                LastMessagePreview = Truncate(last?.Message),
                LastMessageSender = last?.SenderType,
                MessageCount = msgs.Count,
                UnreadMessageCount = msgs.Count(m => !m.IsRead && m.SenderType == "customer"),
                CreatedAt = session.CreatedAt
            };
            return Response<ChatSessionDTO>.SuccessResponse(dto, "Session loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSession failed");
            return Response<ChatSessionDTO>.Fail("Unable to load session.");
        }
    }

    private static bool CanAccessSession(ChatSessionEntity session, Guid? userId)
    {
        if (session.ExternalCustomerId is null) return true;
        return userId.HasValue && session.ExternalCustomerId == userId;
    }

    private static ChatMessageDTO ToMessageDto(ChatMessageEntity m) => new()
    {
        Id = m.Id,
        SessionId = m.SessionId,
        SenderType = m.SenderType,
        SenderName = m.SenderName ?? "Unknown",
        Message = m.Message,
        CreatedAt = m.CreatedAt,
        IsRead = m.IsRead
    };

    private static string? Truncate(string? message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        return message.Length > 50 ? message[..50] + "..." : message;
    }
}
