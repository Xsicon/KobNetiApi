using System.Text.Json;
using Postgrest.Attributes;
using Postgrest.Models;
using static Postgrest.Constants;

namespace KobNeti.Api.Staff;

[Table("staff_profiles")]
public class SbStaffProfile : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("display_name")] public string? DisplayName { get; set; }
    [Column("role")] public string Role { get; set; } = StaffRoles.Support;
    [Column("active")] public bool Active { get; set; } = true;
    [Column("status")] public string Status { get; set; } = StaffStatuses.Active;
    [Column("last_active_at")] public DateTime? LastActiveAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("roles_json")] public string RolesJson { get; set; } = "[]";
}

[Table("staff_profiles")]
public class SbStaffProfileCore : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("user_id")] public Guid? UserId { get; set; }
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("display_name")] public string? DisplayName { get; set; }
    [Column("role")] public string Role { get; set; } = StaffRoles.Support;
    [Column("active")] public bool Active { get; set; } = true;
}

[Table("staff_product_access")]
public class SbStaffProductAccess : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("staff_id")] public Guid StaffId { get; set; }
    [Column("product_slug")] public string ProductSlug { get; set; } = string.Empty;
}

[Table("staff_invites")]
public class SbStaffInvite : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("staff_id")] public Guid? StaffId { get; set; }
    [Column("email")] public string Email { get; set; } = string.Empty;
    [Column("display_name")] public string? DisplayName { get; set; }
    [Column("role")] public string Role { get; set; } = StaffRoles.Support;
    [Column("roles_json")] public string RolesJson { get; set; } = "[]";
    [Column("product_slugs_json")] public string ProductSlugsJson { get; set; } = "[]";
    [Column("invited_by_email")] public string? InvitedByEmail { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("accepted_at")] public DateTime? AcceptedAt { get; set; }
    [Column("cancelled_at")] public DateTime? CancelledAt { get; set; }
}

public class SupabaseStaffDirectory : IStaffDirectory
{
    private readonly Supabase.Client _client;
    private readonly InMemoryStaffDirectory _configFallback;
    private readonly ILogger<SupabaseStaffDirectory> _logger;

    public SupabaseStaffDirectory(
        Supabase.Client client,
        InMemoryStaffDirectory configFallback,
        ILogger<SupabaseStaffDirectory> logger)
    {
        _client = client;
        _configFallback = configFallback;
        _logger = logger;
    }

    public async Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            var response = await _client.From<SbStaffProfileCore>()
                .Filter("email", Operator.Equals, normalized)
                .Filter("active", Operator.Equals, "true")
                .Get();

            var core = (response.Models ?? []).FirstOrDefault();
            if (core is null && !string.Equals(email.Trim(), normalized, StringComparison.Ordinal))
            {
                response = await _client.From<SbStaffProfileCore>()
                    .Filter("email", Operator.Equals, email.Trim())
                    .Filter("active", Operator.Equals, "true")
                    .Get();
                core = (response.Models ?? []).FirstOrDefault();
            }
            if (core is null)
                return await _configFallback.FindByEmailAsync(normalized, ct);

            return await MapWithProductsAsync(await LoadFullOrCoreAsync(core, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff directory DB read failed for {Email}; using config.", normalized);
            return await _configFallback.FindByEmailAsync(normalized, ct);
        }
    }

    public async Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default)
    {
        try
        {
            List<SbStaffProfile> profiles;
            try
            {
                var response = await _client.From<SbStaffProfile>()
                    .Order("email", Ordering.Ascending)
                    .Get();
                profiles = response.Models ?? [];
            }
            catch
            {
                var core = await _client.From<SbStaffProfileCore>()
                    .Order("email", Ordering.Ascending)
                    .Get();
                profiles = (core.Models ?? []).Select(ToFull).ToList();
            }

            var list = new List<StaffAccessRecord>();
            foreach (var profile in profiles)
                list.Add(await MapWithProductsAsync(profile, ct));
            return await MergeConfiguredStaffAsync(list, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff directory list failed; using config.");
            return await _configFallback.ListAsync(ct);
        }
    }

    private async Task<IReadOnlyList<StaffAccessRecord>> MergeConfiguredStaffAsync(
        List<StaffAccessRecord> list,
        CancellationToken ct)
    {
        try
        {
            var extra = await _configFallback.ListAsync(ct);
            foreach (var staff in extra)
            {
                if (list.Any(s => string.Equals(s.Email, staff.Email, StringComparison.OrdinalIgnoreCase)))
                    continue;
                list.Add(staff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Config staff merge skipped.");
        }

        list.Sort((a, b) => string.Compare(a.Email, b.Email, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public async Task<StaffAccessRecord> InviteAsync(
        string email,
        string? displayName,
        string role,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default,
        IReadOnlyList<string>? roles = null,
        string? invitedByEmail = null,
        bool recordInvite = true)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.");

        var normalizedRoles = StaffRoles.NormalizeList(roles, role);
        var primary = StaffRoles.Primary(normalizedRoles);
        var slugs = NormalizeSlugs(productSlugs);

        try
        {
            var existing = await _client.From<SbStaffProfileCore>()
                .Filter("email", Operator.Equals, normalizedEmail)
                .Get();

            var core = (existing.Models ?? []).FirstOrDefault();
            SbStaffProfile profile;
            if (core is null)
            {
                profile = new SbStaffProfile
                {
                    Id = Guid.NewGuid(),
                    Email = normalizedEmail,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                    Role = primary,
                    Active = true,
                    Status = StaffStatuses.Active,
                    RolesJson = StaffRoles.ToJson(normalizedRoles),
                    CreatedAt = DateTime.UtcNow
                };
                try
                {
                    var inserted = await _client.From<SbStaffProfile>().Insert(profile);
                    profile = (inserted.Models ?? []).FirstOrDefault() ?? profile;
                }
                catch
                {
                    var inserted = await _client.From<SbStaffProfileCore>().Insert(ToCore(profile));
                    var saved = (inserted.Models ?? []).FirstOrDefault();
                    profile = saved is null ? profile : ToFull(saved);
                    profile.Status = StaffStatuses.Active;
                    profile.RolesJson = StaffRoles.ToJson(normalizedRoles);
                }
            }
            else
            {
                core.DisplayName = string.IsNullOrWhiteSpace(displayName) ? core.DisplayName : displayName.Trim();
                core.Role = primary;
                core.Active = true;
                await UpdateCoreAsync(core);
                profile = await LoadFullOrCoreAsync(core, ct);
                profile.Status = StaffStatuses.Active;
                profile.RolesJson = StaffRoles.ToJson(normalizedRoles);
                profile.Active = true;
                await TryUpdateFullAsync(profile);
            }

            await ReplaceProductAccessAsync(profile.Id, slugs, ct);
            if (recordInvite)
                await TryUpsertInviteAsync(profile.Id, normalizedEmail, profile.DisplayName, primary, normalizedRoles, slugs, invitedByEmail, ct);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff invite DB write failed; using in-memory fallback.");
            return await _configFallback.InviteAsync(normalizedEmail, displayName, primary, slugs, ct, normalizedRoles, invitedByEmail, recordInvite);
        }
    }

    public Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default) =>
        SetStatusAsync(staffId, active ? StaffStatuses.Active : StaffStatuses.Deactivated, ct);

    public async Task<StaffAccessRecord?> SetStatusAsync(Guid staffId, string status, CancellationToken ct = default)
    {
        var normalized = StaffStatuses.Normalize(status);
        try
        {
            var profile = await GetByIdAsync(staffId, ct);
            if (profile is null)
                return await _configFallback.SetStatusAsync(staffId, normalized, ct);

            profile.Status = normalized;
            profile.Active = StaffStatuses.ToActiveFlag(normalized);
            await TryUpdateFullAsync(profile);
            await UpdateCoreAsync(ToCore(profile));
            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff status DB write failed for {StaffId}", staffId);
            return await _configFallback.SetStatusAsync(staffId, normalized, ct);
        }
    }

    public async Task<StaffAccessRecord?> SetProductAccessAsync(
        Guid staffId,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default)
    {
        var slugs = NormalizeSlugs(productSlugs);
        try
        {
            var profile = await GetByIdAsync(staffId, ct);
            if (profile is null)
                return await _configFallback.SetProductAccessAsync(staffId, slugs, ct);

            await ReplaceProductAccessAsync(staffId, slugs, ct);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff product access DB write failed for {StaffId}", staffId);
            return await _configFallback.SetProductAccessAsync(staffId, slugs, ct);
        }
    }

    public async Task<StaffAccessRecord?> UpdateProfileAsync(
        Guid staffId,
        string? displayName,
        IReadOnlyList<string>? roles,
        IReadOnlyList<string>? productSlugs,
        string? status,
        CancellationToken ct = default)
    {
        try
        {
            var profile = await GetByIdAsync(staffId, ct);
            if (profile is null)
                return await _configFallback.UpdateProfileAsync(staffId, displayName, roles, productSlugs, status, ct);

            if (displayName is not null)
                profile.DisplayName = string.IsNullOrWhiteSpace(displayName) ? profile.DisplayName : displayName.Trim();
            if (roles is not null)
            {
                var normalizedRoles = StaffRoles.NormalizeList(roles, profile.Role);
                profile.Role = StaffRoles.Primary(normalizedRoles);
                profile.RolesJson = StaffRoles.ToJson(normalizedRoles);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                profile.Status = StaffStatuses.Normalize(status);
                profile.Active = StaffStatuses.ToActiveFlag(profile.Status);
            }

            await TryUpdateFullAsync(profile);
            await UpdateCoreAsync(ToCore(profile));
            if (productSlugs is not null)
                await ReplaceProductAccessAsync(staffId, NormalizeSlugs(productSlugs), ct);
            return await MapWithProductsAsync(profile, ct);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff profile update failed for {StaffId}", staffId);
            return await _configFallback.UpdateProfileAsync(staffId, displayName, roles, productSlugs, status, ct);
        }
    }

    public Task TouchLastActiveAsync(string email, CancellationToken ct = default) =>
        TouchLastActiveAsync(email, null, ct);

    public async Task TouchLastActiveAsync(string email, Guid? authUserId, CancellationToken ct = default)
    {
        var raw = email.Trim();
        var normalized = raw.ToLowerInvariant();
        try
        {
            var response = await _client.From<SbStaffProfile>().Get();
            var profile = (response.Models ?? []).FirstOrDefault(p =>
                string.Equals(p.Email, raw, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Email, normalized, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                profile.LastActiveAt = DateTime.UtcNow;
                if (authUserId is { } uid && uid != Guid.Empty)
                    profile.UserId = uid;
                await TryUpdateFullAsync(profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Touch last-active failed for {Email}", normalized);
        }

        try
        {
            var invites = await _client.From<SbStaffInvite>().Get();
            var open = (invites.Models ?? []).Where(i =>
                (string.Equals(i.Email, raw, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(i.Email, normalized, StringComparison.OrdinalIgnoreCase))
                && i.AcceptedAt is null
                && i.CancelledAt is null);
            foreach (var invite in open)
            {
                invite.AcceptedAt = DateTime.UtcNow;
                await _client.From<SbStaffInvite>()
                    .Filter("id", Operator.Equals, invite.Id.ToString())
                    .Update(invite);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Marking invite accepted failed for {Email}", normalized);
            await _configFallback.TouchLastActiveAsync(normalized, authUserId, ct);
        }
    }

    public async Task<IReadOnlyList<StaffInviteRecord>> ListInvitesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbStaffInvite>()
                .Order("created_at", Ordering.Descending)
                .Get();
            var models = response.Models ?? [];
            await TryCloseInvitesForActiveStaffAsync(models, ct);
            return models.Select(MapInvite).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff invites list failed. Apply supabase/staff_users_ops.sql.");
            return [];
        }
    }

    public async Task<bool> CancelInviteAsync(Guid inviteId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbStaffInvite>()
                .Filter("id", Operator.Equals, inviteId.ToString())
                .Get();
            var invite = (response.Models ?? []).FirstOrDefault();
            if (invite is null)
                return await _configFallback.CancelInviteAsync(inviteId, ct);
            invite.CancelledAt = DateTime.UtcNow;
            await _client.From<SbStaffInvite>()
                .Filter("id", Operator.Equals, inviteId.ToString())
                .Update(invite);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cancel invite failed for {InviteId}", inviteId);
            return await _configFallback.CancelInviteAsync(inviteId, ct);
        }
    }

    public async Task<StaffInviteRecord?> ResendInviteAsync(Guid inviteId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.From<SbStaffInvite>()
                .Filter("id", Operator.Equals, inviteId.ToString())
                .Get();
            var invite = (response.Models ?? []).FirstOrDefault();
            if (invite is null)
                return await _configFallback.ResendInviteAsync(inviteId, ct);

            var mapped = MapInvite(invite);
            if (!mapped.IsPending)
                return null;

            invite.ExpiresAt = DateTime.UtcNow.AddDays(7);
            await _client.From<SbStaffInvite>()
                .Filter("id", Operator.Equals, inviteId.ToString())
                .Update(invite);
            return MapInvite(invite);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resend invite failed for {InviteId}", inviteId);
            return await _configFallback.ResendInviteAsync(inviteId, ct);
        }
    }

    private async Task TryUpsertInviteAsync(
        Guid staffId,
        string email,
        string? displayName,
        string role,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> slugs,
        string? invitedByEmail,
        CancellationToken ct)
    {
        try
        {
            var existing = await _client.From<SbStaffInvite>()
                .Filter("email", Operator.Equals, email)
                .Get();
            var open = (existing.Models ?? []).FirstOrDefault(i => i.AcceptedAt is null && i.CancelledAt is null);
            if (open is not null)
            {
                open.StaffId = staffId;
                open.DisplayName = displayName;
                open.Role = role;
                open.RolesJson = StaffRoles.ToJson(roles);
                open.ProductSlugsJson = JsonSerializer.Serialize(slugs);
                open.ExpiresAt = DateTime.UtcNow.AddDays(7);
                open.InvitedByEmail = invitedByEmail ?? open.InvitedByEmail;
                await _client.From<SbStaffInvite>()
                    .Filter("id", Operator.Equals, open.Id.ToString())
                    .Update(open);
                return;
            }

            await _client.From<SbStaffInvite>().Insert(new SbStaffInvite
            {
                Id = Guid.NewGuid(),
                StaffId = staffId,
                Email = email,
                DisplayName = displayName,
                Role = role,
                RolesJson = StaffRoles.ToJson(roles),
                ProductSlugsJson = JsonSerializer.Serialize(slugs),
                InvitedByEmail = invitedByEmail,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Staff invite row write failed for {Email}. Apply supabase/staff_users_ops.sql.", email);
        }
    }

    private async Task<SbStaffProfile?> GetByIdAsync(Guid staffId, CancellationToken ct)
    {
        try
        {
            var response = await _client.From<SbStaffProfile>()
                .Filter("id", Operator.Equals, staffId.ToString())
                .Get();
            var profile = (response.Models ?? []).FirstOrDefault();
            if (profile is not null)
                return profile;
        }
        catch
        {
            // fall through to core model
        }

        var core = await _client.From<SbStaffProfileCore>()
            .Filter("id", Operator.Equals, staffId.ToString())
            .Get();
        var hit = (core.Models ?? []).FirstOrDefault();
        return hit is null ? null : ToFull(hit);
    }

    private async Task<SbStaffProfile> LoadFullOrCoreAsync(SbStaffProfileCore core, CancellationToken ct)
    {
        var full = await GetByIdAsync(core.Id, ct);
        return full ?? ToFull(core);
    }

    private async Task TryUpdateFullAsync(SbStaffProfile profile)
    {
        try
        {
            await _client.From<SbStaffProfile>()
                .Filter("id", Operator.Equals, profile.Id.ToString())
                .Update(profile);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Full staff profile update skipped for {StaffId}", profile.Id);
        }
    }

    private async Task UpdateCoreAsync(SbStaffProfileCore core)
    {
        await _client.From<SbStaffProfileCore>()
            .Filter("id", Operator.Equals, core.Id.ToString())
            .Update(core);
    }

    private async Task ReplaceProductAccessAsync(Guid staffId, IReadOnlyList<string> slugs, CancellationToken ct)
    {
        var existing = await _client.From<SbStaffProductAccess>()
            .Filter("staff_id", Operator.Equals, staffId.ToString())
            .Get();

        foreach (var row in existing.Models ?? [])
        {
            await _client.From<SbStaffProductAccess>()
                .Filter("id", Operator.Equals, row.Id.ToString())
                .Delete();
        }

        foreach (var slug in slugs)
        {
            await _client.From<SbStaffProductAccess>().Insert(new SbStaffProductAccess
            {
                Id = Guid.NewGuid(),
                StaffId = staffId,
                ProductSlug = slug
            });
        }
    }

    private async Task<StaffAccessRecord> MapWithProductsAsync(SbStaffProfile profile, CancellationToken ct)
    {
        var access = await _client.From<SbStaffProductAccess>()
            .Filter("staff_id", Operator.Equals, profile.Id.ToString())
            .Get();

        var slugs = (access.Models ?? [])
            .Select(a => a.ProductSlug)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = StaffRoles.FromJson(profile.RolesJson, profile.Role);
        var status = StaffStatuses.Normalize(profile.Status, profile.Active);
        return new StaffAccessRecord
        {
            Id = profile.Id,
            UserId = profile.UserId,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            Role = StaffRoles.Primary(roles),
            Roles = roles,
            Status = status,
            Active = StaffStatuses.ToActiveFlag(status),
            ProductSlugs = slugs,
            CreatedAt = profile.CreatedAt,
            LastActiveAt = profile.LastActiveAt
        };
    }

    private async Task TryCloseInvitesForActiveStaffAsync(List<SbStaffInvite> invites, CancellationToken ct)
    {
        var pending = invites.Where(i => i.AcceptedAt is null && i.CancelledAt is null).ToList();
        if (pending.Count == 0)
            return;

        try
        {
            var profiles = await _client.From<SbStaffProfile>().Get();
            var active = (profiles.Models ?? [])
                .Where(p => p.LastActiveAt is not null || (p.UserId is { } uid && uid != Guid.Empty))
                .ToList();
            if (active.Count == 0)
                return;

            foreach (var invite in pending)
            {
                var profile = active.FirstOrDefault(p =>
                    string.Equals(p.Email, invite.Email, StringComparison.OrdinalIgnoreCase));
                if (profile is null)
                    continue;

                invite.AcceptedAt = profile.LastActiveAt ?? DateTime.UtcNow;
                await _client.From<SbStaffInvite>()
                    .Filter("id", Operator.Equals, invite.Id.ToString())
                    .Update(invite);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Invite reconcile skipped");
        }
    }

    private static StaffInviteRecord MapInvite(SbStaffInvite i)
    {
        var roles = StaffRoles.FromJson(i.RolesJson, i.Role);
        List<string> slugs;
        try
        {
            slugs = JsonSerializer.Deserialize<List<string>>(i.ProductSlugsJson) ?? [];
        }
        catch
        {
            slugs = [];
        }

        return new StaffInviteRecord
        {
            Id = i.Id,
            StaffId = i.StaffId,
            Email = i.Email,
            DisplayName = i.DisplayName,
            Role = StaffRoles.Primary(roles),
            Roles = roles,
            ProductSlugs = slugs,
            InvitedByEmail = i.InvitedByEmail,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            AcceptedAt = i.AcceptedAt,
            CancelledAt = i.CancelledAt
        };
    }

    private static SbStaffProfile ToFull(SbStaffProfileCore core) => new()
    {
        Id = core.Id,
        UserId = core.UserId,
        Email = core.Email,
        DisplayName = core.DisplayName,
        Role = core.Role,
        Active = core.Active,
        Status = core.Active ? StaffStatuses.Active : StaffStatuses.Deactivated,
        RolesJson = StaffRoles.ToJson([core.Role])
    };

    private static SbStaffProfileCore ToCore(SbStaffProfile profile) => new()
    {
        Id = profile.Id,
        UserId = profile.UserId,
        Email = profile.Email,
        DisplayName = profile.DisplayName,
        Role = profile.Role,
        Active = profile.Active
    };

    private static IReadOnlyList<string> NormalizeSlugs(IEnumerable<string>? slugs) =>
        (slugs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
