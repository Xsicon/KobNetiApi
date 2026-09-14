using System.Collections.Concurrent;
using System.Text.Json;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Services;

/// <summary>
/// Idempotent demo seed for Engineering (board, milestones, GitHub cache).
/// Runs once per tenant when the task board is empty.
/// </summary>
public static class EngineeringSampleData
{
    private static readonly HashSet<string> DemoTenants = new(StringComparer.OrdinalIgnoreCase)
    {
        "muuqwear", "salguri", "gaarx"
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SeedLocks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task EnsureSeededAsync(
        ISupportStore store,
        string tenantId,
        IStaffDirectory? staffDirectory = null,
        CancellationToken ct = default)
    {
        if (!DemoTenants.Contains(tenantId))
            return;

        var gate = SeedLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var roster = staffDirectory is null
                ? (IReadOnlyList<StaffAccessRecord>)[]
                : await staffDirectory.ListAsync(ct);
            await SeedLockedAsync(store, tenantId, roster);
            await RemapAssigneesToStaffAsync(store, tenantId, roster);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task SeedLockedAsync(
        ISupportStore store,
        string tenantId,
        IReadOnlyList<StaffAccessRecord> roster)
    {
        var (_, total) = await store.ListEngTasksAsync(tenantId, null, null, 1, 500);
        if (total > 0)
            return;

        var people = RosterForTenant(roster, tenantId);
        StaffPick Pick(int i) => people.Count == 0 ? default : people[i % people.Count];

        var brand = BrandLabel(tenantId);
        var now = DateTime.UtcNow;
        var existing = await store.ListMilestonesAsync(tenantId);

        // PostgREST omits client ids on insert ([PrimaryKey(..., false)]), so always
        // keep the row returned from the store — never the Guid generated in memory.
        var msHoliday = await EnsureMilestoneAsync(store, existing, tenantId, $"{brand} Holiday Drop", "active",
            now.AddDays(18), now.AddDays(-7), 1,
            "Launch festive collection, promo banners, and inventory sync with warehouse.");
        var msCheckout = await EnsureMilestoneAsync(store, existing, tenantId, "Checkout & Payments Hardening", "active",
            now.AddDays(8), now.AddDays(-14), 2,
            "3DS2, address validation, and payment retry logic before peak traffic.");
        var msChat = await EnsureMilestoneAsync(store, existing, tenantId, "Support Widget v2", "planned",
            now.AddDays(35), now.AddDays(5), 3,
            "Sticky notes, escalate-to-incident, and unread badge parity with KobNeti Support Hub.");
        var msReturns = await EnsureMilestoneAsync(store, existing, tenantId, "Returns & Size Guide", "completed",
            now.AddDays(-28), now.AddDays(-45), 0,
            "Self-serve returns portal and bilingual size charts shipped.");

        await SyncCalendarAsync(store, tenantId, msHoliday);
        await SyncCalendarAsync(store, tenantId, msCheckout);
        await SyncCalendarAsync(store, tenantId, msChat);
        await SyncCalendarAsync(store, tenantId, msReturns);

        var seq = 0;
        var p0 = Pick(0);
        var p1 = Pick(1);
        var p2 = Pick(2);
        var p3 = Pick(3);
        await InsertTaskAsync(store, tenantId, ++seq, "Add Somali + Arabic size chart PDFs", "feature", "backlog", "medium",
            3, p0.Name, p0.UserId, null, null, now.AddDays(-2));
        await InsertTaskAsync(store, tenantId, ++seq, "M-Pesa refund webhook reconciliation", "feature", "backlog", "high",
            5, p1.Name, p1.UserId, null, null, now.AddDays(-1));
        await InsertTaskAsync(store, tenantId, ++seq, "Wishlist share links for Instagram stories", "feature", "backlog", "low",
            2, null, null, null, null, now.AddDays(-3));

        await InsertTaskAsync(store, tenantId, ++seq, "Fix Safari address autofill on checkout", "bug", "ready", "high",
            2, p2.Name, p2.UserId, msCheckout.Id, null, now.AddDays(-1));
        await InsertTaskAsync(store, tenantId, ++seq, "Product API: cache stock counts per variant", "chore", "ready", "medium",
            3, p1.Name, p1.UserId, msHoliday.Id, null, now);

        await InsertTaskAsync(store, tenantId, ++seq, "Live chat handoff uses Support Hub sticky notes", "feature", "in_progress", "critical",
            5, p0.Name, p0.UserId, msChat.Id, null, now);
        await InsertTaskAsync(store, tenantId, ++seq, "CDN cache headers for lookbook images", "chore", "in_progress", "medium",
            2, p3.Name, p3.UserId, msHoliday.Id, null, now.AddHours(-6));

        var prUrl = tenantId.Equals("muuqwear", StringComparison.OrdinalIgnoreCase)
            ? "https://github.com/kobneti/muuqwear-web/pull/142"
            : null;
        await InsertTaskAsync(store, tenantId, ++seq, "Stripe 3DS2 for international cards", "feature", "in_review", "critical",
            5, p2.Name, p2.UserId, msCheckout.Id, prUrl, now.AddHours(-12));

        await InsertTaskAsync(store, tenantId, ++seq, "Deploy Ramadan promo landing page", "feature", "done", "high",
            3, p0.Name, p0.UserId, msHoliday.Id, null, now.AddDays(-10));
        await InsertTaskAsync(store, tenantId, ++seq, "Fix guest cart merge null reference", "bug", "done", "high",
            2, p1.Name, p1.UserId, msCheckout.Id, null, now.AddDays(-7));
        await InsertTaskAsync(store, tenantId, ++seq, "Returns portal SLA dashboard tiles", "feature", "done", "medium",
            3, p3.Name, p3.UserId, msReturns.Id, null, now.AddDays(-20));

        if (tenantId.Equals("salguri", StringComparison.OrdinalIgnoreCase))
        {
            await InsertTaskAsync(store, tenantId, ++seq, "Wholesale price tier API", "feature", "in_progress", "high",
                5, p2.Name, p2.UserId, msCheckout.Id, null, now);
        }

        if (tenantId.Equals("gaarx", StringComparison.OrdinalIgnoreCase))
        {
            await InsertTaskAsync(store, tenantId, ++seq, "Driver GPS socket reconnect backoff", "bug", "in_review", "critical",
                3, p3.Name, p3.UserId, msChat.Id, null, now);
        }

        await SeedGithubCacheAsync(store, tenantId, now);
    }

    private static string BrandLabel(string tenantId) => tenantId.ToLowerInvariant() switch
    {
        "muuqwear" => "MuuqWear",
        "salguri" => "Salguri",
        "gaarx" => "GaarX",
        _ => tenantId
    };

    private static async Task<EngMilestoneEntity> EnsureMilestoneAsync(
        ISupportStore store,
        List<EngMilestoneEntity> existing,
        string tenantId,
        string title,
        string status,
        DateTime targetDateUtc,
        DateTime startDateUtc,
        int sortOrder,
        string? description)
    {
        var found = existing.FirstOrDefault(m =>
            string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase));
        if (found is not null)
            return found;

        var now = DateTime.UtcNow;
        var saved = await store.InsertMilestoneAsync(new EngMilestoneEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Description = description,
            Status = status,
            TargetDate = DateOnly.FromDateTime(targetDateUtc),
            StartDate = DateOnly.FromDateTime(startDateUtc),
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        });

        if (await store.GetMilestoneAsync(tenantId, saved.Id) is null)
        {
            var listed = await store.ListMilestonesAsync(tenantId);
            saved = listed.LastOrDefault(m =>
                string.Equals(m.Title, title, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Failed to persist milestone '{title}' for {tenantId}.");
        }

        existing.Add(saved);
        return saved;
    }

    private static async Task SyncCalendarAsync(ISupportStore store, string tenantId, EngMilestoneEntity milestone)
    {
        if (!milestone.TargetDate.HasValue || milestone.CalendarEventId.HasValue)
            return;

        var starts = milestone.TargetDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var evt = new CalendarEventEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = $"Milestone: {milestone.Title}",
            Description = milestone.Description,
            EventType = "milestone",
            StartsAt = starts,
            EndsAt = starts.AddDays(1),
            SourceEntityType = "eng_milestone",
            SourceEntityId = milestone.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await store.UpsertCalendarEventAsync(evt);
        milestone.CalendarEventId = evt.Id;
        await store.UpdateMilestoneAsync(milestone);
    }

    private static async Task InsertTaskAsync(
        ISupportStore store,
        string tenantId,
        int seq,
        string title,
        string type,
        string status,
        string priority,
        decimal points,
        string? assignee,
        Guid? assigneeUserId,
        Guid? milestoneId,
        string? prUrl,
        DateTime updatedAt)
    {
        var created = updatedAt.AddDays(-Math.Clamp(seq, 2, 12));
        var task = new EngTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskNumber = $"TASK-SEED-{seq:D4}",
            Title = title,
            TaskType = type,
            Status = status,
            Priority = priority,
            EstimatePoints = points,
            AssigneeName = assignee,
            AssigneeUserId = assigneeUserId,
            MilestoneId = milestoneId,
            GithubPrUrl = prUrl,
            CreatedAt = created,
            UpdatedAt = updatedAt
        };
        try
        {
            await store.InsertEngTaskAsync(task);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Partial seed from a previous run — skip this row.
        }
    }

    private static bool IsDuplicateKey(Exception ex) =>
        ex.Message.Contains("23505", StringComparison.Ordinal)
        || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);

    private readonly record struct StaffPick(string? Name, Guid? UserId);

    private static List<StaffPick> RosterForTenant(IReadOnlyList<StaffAccessRecord> roster, string tenantId)
    {
        var active = roster.Where(s => s.Active && StaffStatuses.IsLoginAllowed(s.Status)).ToList();
        var scoped = active.Where(s =>
            s.ProductSlugs.Count == 0
            || s.ProductSlugs.Any(p => string.Equals(p, tenantId, StringComparison.OrdinalIgnoreCase))
            || s.Roles.Any(r => string.Equals(r, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase))
            || string.Equals(s.Role, StaffRoles.Admin, StringComparison.OrdinalIgnoreCase)).ToList();
        if (scoped.Count == 0) scoped = active;
        return scoped
            .Select(s => new StaffPick(StaffLabel(s), s.UserId ?? s.Id))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToList();
    }

    private static async Task RemapAssigneesToStaffAsync(
        ISupportStore store,
        string tenantId,
        IReadOnlyList<StaffAccessRecord> roster)
    {
        var people = RosterForTenant(roster, tenantId);
        if (people.Count == 0) return;

        var (tasks, _) = await store.ListEngTasksAsync(tenantId, null, null, 1, 500);
        var i = 0;
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.AssigneeName))
                continue;
            if (MatchesStaff(task.AssigneeName, roster))
                continue;

            var pick = people[i++ % people.Count];
            task.AssigneeName = pick.Name;
            task.AssigneeUserId = pick.UserId;
            task.UpdatedAt = DateTime.UtcNow;
            await store.UpdateEngTaskAsync(task);
        }
    }

    private static bool MatchesStaff(string? assignee, IReadOnlyList<StaffAccessRecord> roster)
    {
        if (string.IsNullOrWhiteSpace(assignee)) return false;
        var value = assignee.Trim();
        return roster.Any(s =>
            string.Equals(StaffLabel(s), value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Email, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Email.Split('@')[0], value, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(s.DisplayName)
                && string.Equals(s.DisplayName, value, StringComparison.OrdinalIgnoreCase)));
    }

    private static string StaffLabel(StaffAccessRecord staff) =>
        string.IsNullOrWhiteSpace(staff.DisplayName) ? staff.Email : staff.DisplayName.Trim();

    private static async Task SeedGithubCacheAsync(ISupportStore store, string tenantId, DateTime now)
    {
        var webRepo = tenantId switch
        {
            "muuqwear" => "https://github.com/kobneti/muuqwear-web",
            "salguri" => "https://github.com/kobneti/salguri-web",
            "gaarx" => "https://github.com/kobneti/gaarx-web",
            _ => $"https://github.com/kobneti/{tenantId}-web"
        };
        var apiRepo = tenantId switch
        {
            "muuqwear" => "https://github.com/kobneti/muuqwear-api",
            "salguri" => "https://github.com/kobneti/salguri-api",
            "gaarx" => "https://github.com/kobneti/gaarx-api",
            _ => $"https://github.com/kobneti/{tenantId}-api"
        };

        var pullsWeb = JsonSerializer.Serialize(new[]
        {
            new { number = 142, title = "feat(checkout): Stripe 3DS2 for international cards", state = "open", html_url = $"{webRepo}/pull/142", author = "Ibrahim M.", updated_at = now.AddHours(-4).ToString("o") },
            new { number = 138, title = "fix(cart): guest merge preserves promo codes", state = "merged", html_url = $"{webRepo}/pull/138", author = "Sarah K.", updated_at = now.AddDays(-2).ToString("o") },
            new { number = 135, title = "ui: Ramadan hero banner + collection grid", state = "closed", html_url = $"{webRepo}/pull/135", author = "Adeel D.", updated_at = now.AddDays(-5).ToString("o") }
        });

        var commitsWeb = JsonSerializer.Serialize(new[]
        {
            new { sha = "e4a91b2c1d0f", message = "fix(checkout): Safari address autofill regression", html_url = $"{webRepo}/commit/e4a91b2", author = "Ibrahim M.", date = now.AddHours(-2).ToString("o") },
            new { sha = "7c8d201a9f3e", message = "feat: live chat widget unread badge sync", html_url = $"{webRepo}/commit/7c8d201", author = "Adeel D.", date = now.AddHours(-18).ToString("o") },
            new { sha = "b11c49eff21a", message = "chore: bump Blazor packages to 9.0.8", html_url = $"{webRepo}/commit/b11c49e", author = "Sarah K.", date = now.AddDays(-1).ToString("o") }
        });

        var pullsApi = JsonSerializer.Serialize(new[]
        {
            new { number = 89, title = "perf: Redis cache layer for variant stock counts", state = "open", html_url = $"{apiRepo}/pull/89", author = "Sarah K.", updated_at = now.AddHours(-6).ToString("o") },
            new { number = 87, title = "feat(chat): escalate session to Support Hub incident", state = "open", html_url = $"{apiRepo}/pull/87", author = "Adeel D.", updated_at = now.AddDays(-1).ToString("o") }
        });

        var commitsApi = JsonSerializer.Serialize(new[]
        {
            new { sha = "f56a12bc8890", message = "fix(orders): M-Pesa callback idempotency key", html_url = $"{apiRepo}/commit/f56a12b", author = "Sarah K.", date = now.AddHours(-8).ToString("o") },
            new { sha = "09da23f7712c", message = "chore: align JWT secret docs with KobNeti bridge", html_url = $"{apiRepo}/commit/09da23f", author = "Adeel D.", date = now.AddDays(-2).ToString("o") }
        });

        await UpsertGithubCacheRowAsync(store, tenantId, ProductRepoKinds.WebApp, webRepo, "pulls", pullsWeb, now.AddHours(-1));
        await UpsertGithubCacheRowAsync(store, tenantId, ProductRepoKinds.WebApp, webRepo, "commits", commitsWeb, now.AddHours(-1));
        await UpsertGithubCacheRowAsync(store, tenantId, ProductRepoKinds.Api, apiRepo, "pulls", pullsApi, now.AddHours(-3));
        await UpsertGithubCacheRowAsync(store, tenantId, ProductRepoKinds.Api, apiRepo, "commits", commitsApi, now.AddHours(-3));
    }

    private static Task UpsertGithubCacheRowAsync(
        ISupportStore store,
        string tenantId,
        string repoKey,
        string repoUrl,
        string cacheKind,
        string payloadJson,
        DateTime fetchedAt) =>
        store.UpsertGithubCacheAsync(new GithubCacheEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RepoKey = repoKey,
            RepoUrl = repoUrl,
            CacheKind = cacheKind,
            PayloadJson = payloadJson,
            FetchedAt = fetchedAt
        });
}
