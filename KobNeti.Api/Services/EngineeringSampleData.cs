using System.Text.Json;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Products;

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

    public static async Task EnsureSeededAsync(ISupportStore store, string tenantId, CancellationToken ct = default)
    {
        if (!DemoTenants.Contains(tenantId))
            return;

        var (_, total) = await store.ListEngTasksAsync(tenantId, null, null, 1, 1);
        if (total > 0)
            return;

        var brand = BrandLabel(tenantId);
        var now = DateTime.UtcNow;

        var msHoliday = await InsertMilestoneAsync(store, tenantId, $"{brand} Holiday Drop", "active",
            now.AddDays(18), now.AddDays(-7), 1,
            "Launch festive collection, promo banners, and inventory sync with warehouse.");
        var msCheckout = await InsertMilestoneAsync(store, tenantId, "Checkout & Payments Hardening", "active",
            now.AddDays(8), now.AddDays(-14), 2,
            "3DS2, address validation, and payment retry logic before peak traffic.");
        var msChat = await InsertMilestoneAsync(store, tenantId, "Support Widget v2", "planned",
            now.AddDays(35), now.AddDays(5), 3,
            "Sticky notes, escalate-to-incident, and unread badge parity with KobNeti Support Hub.");
        var msReturns = await InsertMilestoneAsync(store, tenantId, "Returns & Size Guide", "completed",
            now.AddDays(-28), now.AddDays(-45), 0,
            "Self-serve returns portal and bilingual size charts shipped.");

        await SyncCalendarAsync(store, tenantId, msHoliday);
        await SyncCalendarAsync(store, tenantId, msCheckout);
        await SyncCalendarAsync(store, tenantId, msChat);
        await SyncCalendarAsync(store, tenantId, msReturns);

        var seq = 0;
        await InsertTaskAsync(store, tenantId, ++seq, "Add Somali + Arabic size chart PDFs", "feature", "backlog", "medium",
            3, "Adeel D.", msReturns.Id, null, now.AddDays(-2));
        await InsertTaskAsync(store, tenantId, ++seq, "M-Pesa refund webhook reconciliation", "feature", "backlog", "high",
            5, "Sarah K.", msCheckout.Id, null, now.AddDays(-1));
        await InsertTaskAsync(store, tenantId, ++seq, "Wishlist share links for Instagram stories", "feature", "backlog", "low",
            2, null, msHoliday.Id, null, now.AddDays(-3));

        await InsertTaskAsync(store, tenantId, ++seq, "Fix Safari address autofill on checkout", "bug", "ready", "high",
            2, "Ibrahim M.", msCheckout.Id, null, now.AddDays(-1));
        await InsertTaskAsync(store, tenantId, ++seq, "Product API: cache stock counts per variant", "chore", "ready", "medium",
            3, "Sarah K.", msHoliday.Id, null, now);

        await InsertTaskAsync(store, tenantId, ++seq, "Live chat handoff uses Support Hub sticky notes", "feature", "in_progress", "critical",
            5, "Adeel D.", msChat.Id, null, now);
        await InsertTaskAsync(store, tenantId, ++seq, "CDN cache headers for lookbook images", "chore", "in_progress", "medium",
            2, "Leila H.", msHoliday.Id, null, now.AddHours(-6));

        var prUrl = tenantId.Equals("muuqwear", StringComparison.OrdinalIgnoreCase)
            ? "https://github.com/kobneti/muuqwear-web/pull/142"
            : null;
        await InsertTaskAsync(store, tenantId, ++seq, "Stripe 3DS2 for international cards", "feature", "in_review", "critical",
            5, "Ibrahim M.", msCheckout.Id, prUrl, now.AddHours(-12));

        await InsertTaskAsync(store, tenantId, ++seq, "Deploy Ramadan promo landing page", "feature", "done", "high",
            3, "Adeel D.", msHoliday.Id, null, now.AddDays(-10));
        await InsertTaskAsync(store, tenantId, ++seq, "Fix guest cart merge null reference", "bug", "done", "high",
            2, "Sarah K.", msCheckout.Id, null, now.AddDays(-7));
        await InsertTaskAsync(store, tenantId, ++seq, "Returns portal SLA dashboard tiles", "feature", "done", "medium",
            3, "Leila H.", msReturns.Id, null, now.AddDays(-20));

        if (tenantId.Equals("salguri", StringComparison.OrdinalIgnoreCase))
        {
            await InsertTaskAsync(store, tenantId, ++seq, "Wholesale price tier API", "feature", "in_progress", "high",
                5, "Mike C.", msCheckout.Id, null, now);
        }

        if (tenantId.Equals("gaarx", StringComparison.OrdinalIgnoreCase))
        {
            await InsertTaskAsync(store, tenantId, ++seq, "Driver GPS socket reconnect backoff", "bug", "in_review", "critical",
                3, "Leila H.", msChat.Id, null, now);
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

    private static async Task<EngMilestoneEntity> InsertMilestoneAsync(
        ISupportStore store,
        string tenantId,
        string title,
        string status,
        DateTime targetDateUtc,
        DateTime startDateUtc,
        int sortOrder,
        string? description)
    {
        var now = DateTime.UtcNow;
        var milestone = new EngMilestoneEntity
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
        };
        await store.InsertMilestoneAsync(milestone);
        return milestone;
    }

    private static async Task SyncCalendarAsync(ISupportStore store, string tenantId, EngMilestoneEntity milestone)
    {
        if (!milestone.TargetDate.HasValue)
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
        Guid? milestoneId,
        string? prUrl,
        DateTime updatedAt)
    {
        var created = updatedAt.AddDays(-Random.Shared.Next(2, 12));
        var task = new EngTaskEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskNumber = $"TASK-{created:yyyyMMdd}-{seq:D4}",
            Title = title,
            TaskType = type,
            Status = status,
            Priority = priority,
            EstimatePoints = points,
            AssigneeName = assignee,
            MilestoneId = milestoneId,
            GithubPrUrl = prUrl,
            CreatedAt = created,
            UpdatedAt = updatedAt
        };
        await store.InsertEngTaskAsync(task);
    }

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
