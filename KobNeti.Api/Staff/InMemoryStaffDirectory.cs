using Microsoft.Extensions.Options;
using KobNeti.Api.Tenancy;

namespace KobNeti.Api.Staff;

public class StaffAssignmentConfig
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = StaffRoles.Support;
    public string? DisplayName { get; set; }
    public string[] ProductSlugs { get; set; } = [];
}

public class InMemoryStaffDirectory : IStaffDirectory
{
    private readonly List<StaffAccessRecord> _staff = [];
    private readonly List<StaffInviteRecord> _invites = [];
    private readonly object _gate = new();

    public InMemoryStaffDirectory(IOptions<SupportOptions> options)
    {
        foreach (var s in options.Value.Staff ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Email))
                continue;
            AddConfigured(s.Email, s.DisplayName, s.Role, s.ProductSlugs);
        }

        foreach (var email in options.Value.CoreAdminEmails ?? [])
        {
            if (string.IsNullOrWhiteSpace(email))
                continue;
            if (_staff.Any(s => string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)))
                continue;
            AddConfigured(email, null, StaffRoles.Admin, []);
        }
    }

    private void AddConfigured(string email, string? displayName, string? role, IEnumerable<string>? productSlugs)
    {
        var roles = StaffRoles.NormalizeList(null, role);
        _staff.Add(new StaffAccessRecord
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            Role = StaffRoles.Primary(roles),
            Roles = roles,
            Status = StaffStatuses.Active,
            Active = true,
            ProductSlugs = NormalizeSlugs(productSlugs),
            CreatedAt = default,
            LastActiveAt = null
        });
    }

    public Task<StaffAccessRecord?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s =>
                s.Active && string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<IReadOnlyList<StaffAccessRecord>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StaffAccessRecord> list = _staff
                .OrderBy(s => s.Email, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .Where(s => s is not null)
                .Cast<StaffAccessRecord>()
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<StaffAccessRecord> InviteAsync(
        string email,
        string? displayName,
        string role,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default,
        IReadOnlyList<string>? roles = null,
        string? invitedByEmail = null,
        bool recordInvite = true)
    {
        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.");

        var normalizedRoles = StaffRoles.NormalizeList(roles, role);
        var primary = StaffRoles.Primary(normalizedRoles);
        var slugs = NormalizeSlugs(productSlugs);

        lock (_gate)
        {
            var existing = _staff.FirstOrDefault(s =>
                string.Equals(s.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.DisplayName = string.IsNullOrWhiteSpace(displayName) ? existing.DisplayName : displayName.Trim();
                existing.Role = primary;
                existing.Roles = normalizedRoles;
                existing.Status = StaffStatuses.Active;
                existing.Active = true;
                existing.ProductSlugs = slugs;
                if (recordInvite)
                    UpsertOpenInvite(existing, invitedByEmail);
                return Task.FromResult(Clone(existing)!);
            }

            var created = new StaffAccessRecord
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                Role = primary,
                Roles = normalizedRoles,
                Status = StaffStatuses.Active,
                Active = true,
                ProductSlugs = slugs,
                CreatedAt = DateTime.UtcNow
            };
            _staff.Add(created);
            if (recordInvite)
                UpsertOpenInvite(created, invitedByEmail);
            return Task.FromResult(Clone(created)!);
        }
    }

    public Task<StaffAccessRecord?> SetActiveAsync(Guid staffId, bool active, CancellationToken ct = default) =>
        SetStatusAsync(staffId, active ? StaffStatuses.Active : StaffStatuses.Deactivated, ct);

    public Task<StaffAccessRecord?> SetStatusAsync(Guid staffId, string status, CancellationToken ct = default)
    {
        var normalized = StaffStatuses.Normalize(status);
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s => s.Id == staffId);
            if (hit is null)
                return Task.FromResult<StaffAccessRecord?>(null);
            ApplyStatus(hit, normalized);
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<StaffAccessRecord?> SetProductAccessAsync(
        Guid staffId,
        IReadOnlyList<string> productSlugs,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s => s.Id == staffId);
            if (hit is null)
                return Task.FromResult<StaffAccessRecord?>(null);
            hit.ProductSlugs = NormalizeSlugs(productSlugs);
            return Task.FromResult(Clone(hit));
        }
    }

    public Task<StaffAccessRecord?> UpdateProfileAsync(
        Guid staffId,
        string? displayName,
        IReadOnlyList<string>? roles,
        IReadOnlyList<string>? productSlugs,
        string? status,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s => s.Id == staffId);
            if (hit is null)
                return Task.FromResult<StaffAccessRecord?>(null);

            if (displayName is not null)
                hit.DisplayName = string.IsNullOrWhiteSpace(displayName) ? hit.DisplayName : displayName.Trim();
            if (roles is not null)
            {
                var normalizedRoles = StaffRoles.NormalizeList(roles, hit.Role);
                hit.Roles = normalizedRoles;
                hit.Role = StaffRoles.Primary(normalizedRoles);
            }
            if (productSlugs is not null)
                hit.ProductSlugs = NormalizeSlugs(productSlugs);
            if (!string.IsNullOrWhiteSpace(status))
                ApplyStatus(hit, StaffStatuses.Normalize(status));
            return Task.FromResult(Clone(hit));
        }
    }

    public Task TouchLastActiveAsync(string email, CancellationToken ct = default) =>
        TouchLastActiveAsync(email, null, ct);

    public Task TouchLastActiveAsync(string email, Guid? authUserId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _staff.FirstOrDefault(s =>
                string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                hit.LastActiveAt = DateTime.UtcNow;
                if (authUserId is { } uid && uid != Guid.Empty)
                    hit.UserId = uid;
            }

            foreach (var invite in _invites.Where(i =>
                string.Equals(i.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)
                && i.AcceptedAt is null
                && i.CancelledAt is null))
            {
                invite.AcceptedAt = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StaffInviteRecord>> ListInvitesAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var invite in _invites.Where(i => i.AcceptedAt is null && i.CancelledAt is null))
            {
                var staff = _staff.FirstOrDefault(s =>
                    string.Equals(s.Email, invite.Email, StringComparison.OrdinalIgnoreCase));
                if (staff is not null && (staff.LastActiveAt is not null || staff.UserId is not null))
                    invite.AcceptedAt = staff.LastActiveAt ?? DateTime.UtcNow;
            }

            IReadOnlyList<StaffInviteRecord> list = _invites
                .OrderByDescending(i => i.CreatedAt)
                .Select(CloneInvite)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<bool> CancelInviteAsync(Guid inviteId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _invites.FirstOrDefault(i => i.Id == inviteId);
            if (hit is null)
                return Task.FromResult(false);
            hit.CancelledAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    public Task<StaffInviteRecord?> ResendInviteAsync(Guid inviteId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var hit = _invites.FirstOrDefault(i => i.Id == inviteId);
            if (hit is null || !hit.IsPending)
                return Task.FromResult<StaffInviteRecord?>(null);
            hit.ExpiresAt = DateTime.UtcNow.AddDays(7);
            return Task.FromResult<StaffInviteRecord?>(CloneInvite(hit));
        }
    }

    private void UpsertOpenInvite(StaffAccessRecord staff, string? invitedByEmail)
    {
        var open = _invites.FirstOrDefault(i =>
            string.Equals(i.Email, staff.Email, StringComparison.OrdinalIgnoreCase)
            && i.AcceptedAt is null
            && i.CancelledAt is null);
        if (open is not null)
        {
            open.StaffId = staff.Id;
            open.DisplayName = staff.DisplayName;
            open.Role = staff.Role;
            open.Roles = staff.Roles.ToList();
            open.ProductSlugs = staff.ProductSlugs.ToList();
            open.ExpiresAt = DateTime.UtcNow.AddDays(7);
            open.InvitedByEmail = invitedByEmail ?? open.InvitedByEmail;
            return;
        }

        _invites.Add(new StaffInviteRecord
        {
            Id = Guid.NewGuid(),
            StaffId = staff.Id,
            Email = staff.Email,
            DisplayName = staff.DisplayName,
            Role = staff.Role,
            Roles = staff.Roles.ToList(),
            ProductSlugs = staff.ProductSlugs.ToList(),
            InvitedByEmail = invitedByEmail,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
    }

    private static void ApplyStatus(StaffAccessRecord hit, string status)
    {
        hit.Status = status;
        hit.Active = StaffStatuses.ToActiveFlag(status);
    }

    private static IReadOnlyList<string> NormalizeSlugs(IEnumerable<string>? slugs) =>
        (slugs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static StaffAccessRecord? Clone(StaffAccessRecord? s) =>
        s is null
            ? null
            : new StaffAccessRecord
            {
                Id = s.Id,
                UserId = s.UserId,
                Email = s.Email,
                DisplayName = s.DisplayName,
                Role = s.Role,
                Roles = s.Roles.ToList(),
                Status = s.Status,
                Active = s.Active,
                ProductSlugs = s.ProductSlugs.ToList(),
                CreatedAt = s.CreatedAt,
                LastActiveAt = s.LastActiveAt
            };

    private static StaffInviteRecord CloneInvite(StaffInviteRecord i) =>
        new()
        {
            Id = i.Id,
            StaffId = i.StaffId,
            Email = i.Email,
            DisplayName = i.DisplayName,
            Role = i.Role,
            Roles = i.Roles.ToList(),
            ProductSlugs = i.ProductSlugs.ToList(),
            InvitedByEmail = i.InvitedByEmail,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            AcceptedAt = i.AcceptedAt,
            CancelledAt = i.CancelledAt
        };
}
