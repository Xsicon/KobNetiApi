using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Storage;
using Microsoft.Extensions.Configuration;

namespace KobNeti.Api.Services;

public interface IAuditService
{
    Task WriteAsync(string tenantId, string action, string entityType, string? entityId,
        Guid? actorUserId, string? actorName, object? before = null, object? after = null);
    Task<Response<List<AuditEventDTO>>> SearchAsync(
        string tenantId, string? action, string? entityType, string? search, int take = 100);
}

public class AuditService : IAuditService
{
    private readonly ISupportStore _store;

    public AuditService(ISupportStore store) => _store = store;

    public async Task WriteAsync(
        string tenantId, string action, string entityType, string? entityId,
        Guid? actorUserId, string? actorName, object? before = null, object? after = null)
    {
        await _store.InsertAuditEventAsync(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<Response<List<AuditEventDTO>>> SearchAsync(
        string tenantId, string? action, string? entityType, string? search, int take = 100)
    {
        var items = await _store.ListAuditEventsAsync(tenantId, action, entityType, search, take);
        var list = items.Select(e => new AuditEventDTO
        {
            Id = e.Id,
            ActorUserId = e.ActorUserId,
            ActorName = e.ActorName,
            Action = e.Action,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            BeforeJson = e.BeforeJson,
            AfterJson = e.AfterJson,
            CreatedAt = e.CreatedAt
        }).ToList();
        return Response<List<AuditEventDTO>>.SuccessResponse(list, "Audit events loaded");
    }
}

public interface IAppNotificationService
{
    Task NotifyAsync(string tenantId, string preferenceKey, string title, string? body,
        Guid? userId = null, string? userName = null, string? sourceType = null, string? sourceId = null, string? linkUrl = null);
    Task<Response<List<NotificationDTO>>> ListAsync(string tenantId, Guid? userId, bool unreadOnly);
    Task<Response<NotificationDTO>> MarkReadAsync(string tenantId, Guid id);
    Task<Response<NotificationPrefsDTO>> GetPrefsAsync(string tenantId, Guid userId);
    Task<Response<NotificationPrefsDTO>> SavePrefsAsync(string tenantId, Guid userId, NotificationPrefsDTO prefs);
}

/// <summary>W6 Module 17 — replaces log-only stub for real in-app notifications.</summary>
public class AppNotificationService : IAppNotificationService, INotificationStub
{
    private readonly ISupportStore _store;
    private readonly ILogger<AppNotificationService> _logger;

    public AppNotificationService(ISupportStore store, ILogger<AppNotificationService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task NotifyAsync(string tenantId, string channel, string subject, string body, CancellationToken ct = default)
    {
        // INotificationStub compatibility (incident escalate etc.)
        var key = channel.Contains("escalat", StringComparison.OrdinalIgnoreCase) ? "escalation"
            : channel.Contains("approv", StringComparison.OrdinalIgnoreCase) ? "approval"
            : channel.Contains("assign", StringComparison.OrdinalIgnoreCase) ? "assign"
            : "escalation";
        await EmitAsync(tenantId, key, subject, body);
    }

    public Task NotifyAsync(
        string tenantId, string preferenceKey, string title, string? body,
        Guid? userId = null, string? userName = null, string? sourceType = null, string? sourceId = null, string? linkUrl = null) =>
        EmitAsync(tenantId, preferenceKey, title, body, userId, userName, sourceType, sourceId, linkUrl);

    private async Task EmitAsync(
        string tenantId, string preferenceKey, string title, string? body,
        Guid? userId = null, string? userName = null, string? sourceType = null, string? sourceId = null, string? linkUrl = null)
    {
        if (userId.HasValue)
        {
            var prefs = await _store.GetNotificationPrefsAsync(tenantId, userId.Value);
            if (prefs is not null && !IsEnabled(prefs, preferenceKey))
            {
                _logger.LogDebug("Notification suppressed by prefs {Key} for {User}", preferenceKey, userId);
                return;
            }
        }

        await _store.InsertNotificationAsync(new NotificationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserName = userName,
            Channel = "in_app",
            Title = title,
            Body = body,
            LinkUrl = linkUrl,
            SourceType = sourceType,
            SourceId = sourceId,
            CreatedAt = DateTime.UtcNow
        });
        _logger.LogInformation("Notification [{Key}] tenant={Tenant} title={Title}", preferenceKey, tenantId, title);
    }

    public async Task<Response<List<NotificationDTO>>> ListAsync(string tenantId, Guid? userId, bool unreadOnly)
    {
        var items = await _store.ListNotificationsAsync(tenantId, userId, unreadOnly);
        return Response<List<NotificationDTO>>.SuccessResponse(items.Select(Map).ToList(), "Notifications loaded");
    }

    public async Task<Response<NotificationDTO>> MarkReadAsync(string tenantId, Guid id)
    {
        var n = await _store.GetNotificationAsync(tenantId, id);
        if (n is null)
            return Response<NotificationDTO>.Fail("Notification not found");
        n.ReadAt = DateTime.UtcNow;
        await _store.UpdateNotificationAsync(n);
        return Response<NotificationDTO>.SuccessResponse(Map(n), "Marked read");
    }

    public async Task<Response<NotificationPrefsDTO>> GetPrefsAsync(string tenantId, Guid userId)
    {
        var prefs = await _store.GetNotificationPrefsAsync(tenantId, userId);
        return Response<NotificationPrefsDTO>.SuccessResponse(prefs is null
            ? new NotificationPrefsDTO { UserId = userId }
            : MapPrefs(prefs), "Preferences loaded");
    }

    public async Task<Response<NotificationPrefsDTO>> SavePrefsAsync(string tenantId, Guid userId, NotificationPrefsDTO prefs)
    {
        var entity = new NotificationPrefsEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            AssignEnabled = prefs.AssignEnabled,
            ApprovalEnabled = prefs.ApprovalEnabled,
            EscalationEnabled = prefs.EscalationEnabled,
            ReminderEnabled = prefs.ReminderEnabled,
            UpdatedAt = DateTime.UtcNow
        };
        await _store.UpsertNotificationPrefsAsync(entity);
        return Response<NotificationPrefsDTO>.SuccessResponse(MapPrefs(entity), "Preferences saved");
    }

    private static bool IsEnabled(NotificationPrefsEntity prefs, string key) => key switch
    {
        "assign" => prefs.AssignEnabled,
        "approval" => prefs.ApprovalEnabled,
        "escalation" => prefs.EscalationEnabled,
        "reminder" => prefs.ReminderEnabled,
        _ => true
    };

    private static NotificationDTO Map(NotificationEntity n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        UserName = n.UserName,
        Channel = n.Channel,
        Title = n.Title,
        Body = n.Body,
        LinkUrl = n.LinkUrl,
        SourceType = n.SourceType,
        SourceId = n.SourceId,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt
    };

    private static NotificationPrefsDTO MapPrefs(NotificationPrefsEntity p) => new()
    {
        UserId = p.UserId,
        AssignEnabled = p.AssignEnabled,
        ApprovalEnabled = p.ApprovalEnabled,
        EscalationEnabled = p.EscalationEnabled,
        ReminderEnabled = p.ReminderEnabled
    };
}

public interface ICalendarOpsService
{
    Task<Response<List<CalendarEventDTO>>> ListAsync(string tenantId);
    Task<Response<CalendarEventDTO>> CreateAsync(string tenantId, CreateCalendarEventDTO request);
    Task<Response<int>> SendRemindersAsync(string tenantId, int withinHours = 48);
}

public class CalendarOpsService : ICalendarOpsService
{
    private readonly ISupportStore _store;
    private readonly IAppNotificationService _notify;

    public CalendarOpsService(ISupportStore store, IAppNotificationService notify)
    {
        _store = store;
        _notify = notify;
    }

    public async Task<Response<List<CalendarEventDTO>>> ListAsync(string tenantId)
    {
        var events = await _store.ListCalendarEventsAsync(tenantId);
        return Response<List<CalendarEventDTO>>.SuccessResponse(events.Select(Map).ToList(), "Calendar loaded");
    }

    public async Task<Response<CalendarEventDTO>> CreateAsync(string tenantId, CreateCalendarEventDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Response<CalendarEventDTO>.Fail("Title is required");
        var now = DateTime.UtcNow;
        var evt = new CalendarEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description,
            EventType = string.IsNullOrWhiteSpace(request.EventType) ? "meeting" : request.EventType.Trim().ToLowerInvariant(),
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt?.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.UpsertCalendarEventAsync(evt);
        return Response<CalendarEventDTO>.SuccessResponse(Map(evt), "Event created");
    }

    public async Task<Response<int>> SendRemindersAsync(string tenantId, int withinHours = 48)
    {
        var now = DateTime.UtcNow;
        var until = now.AddHours(withinHours);
        var events = await _store.ListCalendarEventsAsync(tenantId);
        var due = events.Where(e => e.StartsAt >= now && e.StartsAt <= until).ToList();
        var count = 0;
        foreach (var e in due)
        {
            await _notify.NotifyAsync(
                tenantId, "reminder",
                $"Reminder: {e.Title}",
                $"Starts {e.StartsAt:u}",
                sourceType: "calendar_event",
                sourceId: e.Id.ToString());
            count++;
        }
        return Response<int>.SuccessResponse(count, "Reminders sent");
    }

    private static CalendarEventDTO Map(CalendarEventEntity e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        EventType = e.EventType,
        StartsAt = e.StartsAt,
        EndsAt = e.EndsAt,
        SourceEntityType = e.SourceEntityType,
        SourceEntityId = e.SourceEntityId
    };
}

public interface IOpsFileService
{
    Task<Response<List<OpsFileDTO>>> ListAsync(string tenantId, string? folderPath);
    Task<Response<OpsFileDTO>> CreateAsync(string tenantId, CreateOpsFileDTO request, Guid? userId, string? userName);
    Task<Response<OpsFileDTO>> UploadAsync(
        string tenantId,
        string folderPath,
        string fileName,
        string contentType,
        Stream content,
        long length,
        Guid? userId,
        string? userName);
    Task<Response<object>> DeleteAsync(string tenantId, Guid id);
}

public class OpsFileService : IOpsFileService
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    private readonly ISupportStore _store;
    private readonly ISupabaseStorageUploader _storage;

    public OpsFileService(ISupportStore store, ISupabaseStorageUploader storage)
    {
        _store = store;
        _storage = storage;
    }

    public async Task<Response<List<OpsFileDTO>>> ListAsync(string tenantId, string? folderPath)
    {
        var files = await _store.ListOpsFilesAsync(tenantId, folderPath);
        return Response<List<OpsFileDTO>>.SuccessResponse(files.Select(Map).ToList(), "Files loaded");
    }

    public async Task<Response<OpsFileDTO>> CreateAsync(
        string tenantId, CreateOpsFileDTO request, Guid? userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return Response<OpsFileDTO>.Fail("File name is required");

        var folder = NormalizeFolder(request.FolderPath);
        var id = Guid.NewGuid();
        var storagePath = $"ops-files/{tenantId}{folder.TrimEnd('/')}/{id:N}_{request.FileName.Trim()}";
        var file = new OpsFileEntity
        {
            Id = id,
            TenantId = tenantId,
            FolderPath = folder,
            FileName = request.FileName.Trim(),
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StoragePath = storagePath,
            PublicUrl = request.PublicUrl,
            CreatedBy = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertOpsFileAsync(file);
        return Response<OpsFileDTO>.SuccessResponse(Map(file), "File registered (metadata only)");
    }

    public async Task<Response<OpsFileDTO>> UploadAsync(
        string tenantId,
        string folderPath,
        string fileName,
        string contentType,
        Stream content,
        long length,
        Guid? userId,
        string? userName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Response<OpsFileDTO>.Fail("File name is required");
        if (length <= 0)
            return Response<OpsFileDTO>.Fail("File is empty");
        if (length > MaxUploadBytes)
            return Response<OpsFileDTO>.Fail("File must be 25 MB or less");

        var folder = NormalizeFolder(folderPath);
        var id = Guid.NewGuid();
        var safeName = Path.GetFileName(fileName.Trim());
        var objectKey = BuildObjectKey(tenantId, folder, id, safeName);
        string? publicUrl = null;
        string message;

        if (_storage.IsConfigured)
        {
            var upload = await _storage.UploadAsync(objectKey, content, contentType);
            if (!upload.Ok)
                return Response<OpsFileDTO>.Fail(upload.Error ?? "Storage upload failed");
            publicUrl = upload.PublicUrl;
            message = "File uploaded";
        }
        else
        {
            message = "File recorded (enable Supabase ServiceRoleKey + ops-files bucket for storage upload)";
        }

        var file = new OpsFileEntity
        {
            Id = id,
            TenantId = tenantId,
            FolderPath = folder,
            FileName = safeName,
            ContentType = contentType,
            SizeBytes = length,
            StoragePath = objectKey,
            PublicUrl = publicUrl,
            CreatedBy = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertOpsFileAsync(file);
        return Response<OpsFileDTO>.SuccessResponse(Map(file), message);
    }

    public async Task<Response<object>> DeleteAsync(string tenantId, Guid id)
    {
        await _store.DeleteOpsFileAsync(tenantId, id);
        return Response<object>.SuccessResponse(new { }, "File deleted");
    }

    private static string NormalizeFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var p = path.Trim().Replace('\\', '/');
        if (!p.StartsWith('/')) p = "/" + p;
        if (!p.EndsWith('/')) p += "/";
        return p;
    }

    private static string BuildObjectKey(string tenantId, string folder, Guid id, string fileName)
    {
        var folderSegment = folder.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        var parts = new List<string> { tenantId.Trim() };
        if (!string.IsNullOrWhiteSpace(folderSegment))
            parts.AddRange(folderSegment.Split('/', StringSplitOptions.RemoveEmptyEntries));
        parts.Add($"{id:N}_{fileName}");
        return string.Join('/', parts);
    }

    private static OpsFileDTO Map(OpsFileEntity f) => new()
    {
        Id = f.Id,
        FolderPath = f.FolderPath,
        FileName = f.FileName,
        ContentType = f.ContentType,
        SizeBytes = f.SizeBytes,
        StoragePath = f.StoragePath,
        PublicUrl = f.PublicUrl,
        CreatedByName = f.CreatedByName,
        CreatedAt = f.CreatedAt
    };
}

public interface IIntegrationService
{
    Task<Response<List<IntegrationDTO>>> ListAsync(string tenantId);
    Task<Response<IntegrationDTO>> ConnectAsync(string tenantId, ConnectIntegrationDTO request);
    Task<Response<IntegrationDTO>> DisconnectAsync(string tenantId, string provider);
}

public class IntegrationService : IIntegrationService
{
    private static readonly string[] Providers = ["github", "email", "slack", "other"];
    private readonly ISupportStore _store;
    private readonly byte[] _key;

    public IntegrationService(ISupportStore store, IConfiguration config)
    {
        _store = store;
        var keyMaterial = config["Support:SecretsEncryptionKey"]
                          ?? "dev-kobneti-secrets-key-32chars!!";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    public async Task<Response<List<IntegrationDTO>>> ListAsync(string tenantId)
    {
        var list = await _store.ListIntegrationsAsync(tenantId);
        var dtos = new List<IntegrationDTO>();
        foreach (var i in list)
        {
            var secrets = await _store.ListIntegrationSecretsAsync(tenantId, i.Provider);
            dtos.Add(Map(i, secrets.Count > 0));
        }
        // Ensure known providers appear
        foreach (var p in Providers)
        {
            if (dtos.All(d => d.Provider != p))
            {
                dtos.Add(new IntegrationDTO
                {
                    Provider = p,
                    DisplayName = char.ToUpperInvariant(p[0]) + p[1..],
                    Status = "disconnected"
                });
            }
        }
        return Response<List<IntegrationDTO>>.SuccessResponse(dtos.OrderBy(d => d.Provider).ToList(), "Integrations loaded");
    }

    public async Task<Response<IntegrationDTO>> ConnectAsync(string tenantId, ConnectIntegrationDTO request)
    {
        var provider = (request.Provider ?? "").Trim().ToLowerInvariant();
        if (!Providers.Contains(provider))
            return Response<IntegrationDTO>.Fail("Invalid provider");

        var now = DateTime.UtcNow;
        var existing = await _store.GetIntegrationAsync(tenantId, provider);
        var entity = existing ?? new IntegrationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider
        };
        entity.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? char.ToUpperInvariant(provider[0]) + provider[1..]
            : request.DisplayName.Trim();
        entity.Status = "connected";
        entity.ConnectedAt = now;
        entity.DisconnectedAt = null;
        entity.UpdatedAt = now;
        await _store.UpsertIntegrationAsync(entity);

        if (request.Secrets is not null)
        {
            foreach (var (k, v) in request.Secrets)
            {
                if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(v))
                    continue;
                await _store.UpsertIntegrationSecretAsync(new IntegrationSecretEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Provider = provider,
                    SecretKey = k.Trim(),
                    Ciphertext = Encrypt(v),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        var secrets = await _store.ListIntegrationSecretsAsync(tenantId, provider);
        return Response<IntegrationDTO>.SuccessResponse(Map(entity, secrets.Count > 0), "Connected");
    }

    public async Task<Response<IntegrationDTO>> DisconnectAsync(string tenantId, string provider)
    {
        provider = provider.Trim().ToLowerInvariant();
        var existing = await _store.GetIntegrationAsync(tenantId, provider);
        if (existing is null)
            return Response<IntegrationDTO>.Fail("Integration not found");

        existing.Status = "disconnected";
        existing.DisconnectedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        await _store.UpsertIntegrationAsync(existing);
        await _store.DeleteIntegrationSecretsAsync(tenantId, provider);
        return Response<IntegrationDTO>.SuccessResponse(Map(existing, false), "Disconnected");
    }

    private string Encrypt(string plain)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }

    private static IntegrationDTO Map(IntegrationEntity i, bool hasSecrets) => new()
    {
        Id = i.Id,
        Provider = i.Provider,
        DisplayName = i.DisplayName,
        Status = i.Status,
        HasSecrets = hasSecrets,
        ConnectedAt = i.ConnectedAt,
        DisconnectedAt = i.DisconnectedAt
    };
}
