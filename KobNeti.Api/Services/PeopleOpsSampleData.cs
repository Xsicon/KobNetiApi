using System.Collections.Concurrent;
using System.Globalization;
using KobNeti.Api.Data;
using KobNeti.Api.DTOs;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Services;

/// <summary>
/// Idempotent payroll demo seed using the live staff roster (not invented employees).
/// Runs when a tenant has no pay-period totals yet.
/// </summary>
public static class PeopleOpsSampleData
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SeedLocks = new(StringComparer.OrdinalIgnoreCase);

    public static async Task EnsureSeededAsync(
        ISupportStore store,
        IPayrollService payroll,
        string tenantId,
        IStaffDirectory? staffDirectory,
        CancellationToken ct = default)
    {
        var gate = SeedLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Demo payroll belongs on KobNeti. Customer products stay empty until real assigned staff is paid there.
            if (!string.Equals(tenantId, "kobneti", StringComparison.OrdinalIgnoreCase))
                return;

            var periods = await store.ListPayPeriodsAsync(tenantId);
            if (periods.Sum(p => p.TotalMinutes) > 0)
                return;

            var roster = staffDirectory is null
                ? (IReadOnlyList<StaffAccessRecord>)[]
                : await staffDirectory.ListAsync(ct);
            var people = DistinctStaff(roster, tenantId);
            if (people.Count == 0)
                return;
            if (people.All(p => p.Email.EndsWith("@test.local", StringComparison.OrdinalIgnoreCase)
                                || p.Email.EndsWith("@tenant.", StringComparison.OrdinalIgnoreCase)))
                return;

            await SeedLockedAsync(store, payroll, tenantId, people, periods);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task SeedLockedAsync(
        ISupportStore store,
        IPayrollService payroll,
        string tenantId,
        IReadOnlyList<StaffAccessRecord> people,
        List<PayPeriodEntity> existingPeriods)
    {
        var now = DateTime.UtcNow;
        var currentHalf = CurrentHalf(now);
        var previousHalf = PreviousHalf(currentHalf.Start);

        var rates = await store.ListPayRatesAsync(tenantId);
        foreach (var person in people)
        {
            var userId = person.UserId ?? person.Id;
            if (rates.Any(r => r.UserId == userId || r.StaffId == person.Id))
                continue;
            await store.InsertPayRateAsync(new PayRateEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StaffId = person.Id,
                UserId = userId,
                Role = StaffRoles.Primary(person.Roles),
                HourlyRate = RateForRole(StaffRoles.Primary(person.Roles)),
                Currency = "USD",
                EffectiveFrom = previousHalf.Start,
                CreatedAt = now
            });
        }

        if (existingPeriods.Count == 0)
        {
            await EnsureTimeAsync(store, tenantId, people, previousHalf.Start, previousHalf.End, 76);
            await EnsureTimeAsync(store, tenantId, people, currentHalf.Start, currentHalf.End, 80);

            var previous = await store.InsertPayPeriodAsync(NewPeriod(
                tenantId, previousHalf.Start, previousHalf.End, now.AddDays(-20)));
            await payroll.CalculateAsync(tenantId, previous.Id);
            previous = await store.GetPayPeriodAsync(tenantId, previous.Id) ?? previous;
            previous.Status = PayPeriodStatus.Exported;
            previous.UpdatedAt = previousHalf.End.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc).AddDays(5);
            await store.UpdatePayPeriodAsync(previous);

            var current = await store.InsertPayPeriodAsync(NewPeriod(
                tenantId, currentHalf.Start, currentHalf.End, now.AddDays(-2)));
            await payroll.CalculateAsync(tenantId, current.Id);
            current = await store.GetPayPeriodAsync(tenantId, current.Id) ?? current;
            var requester = people[0];
            await payroll.SubmitForApprovalAsync(
                tenantId,
                current.Id,
                requester.UserId ?? requester.Id,
                StaffLabel(requester));
            return;
        }

        foreach (var period in existingPeriods)
        {
            var hours = period.EndsOn.Day <= 15 ? 80 : 76;
            await EnsureTimeAsync(store, tenantId, people, period.StartsOn, period.EndsOn, hours);
            if (period.Status is PayPeriodStatus.Open or PayPeriodStatus.Calculated)
                await payroll.CalculateAsync(tenantId, period.Id);
        }
    }

    private static PayPeriodEntity NewPeriod(string tenantId, DateOnly start, DateOnly end, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Label = FormatRange(start, end),
        StartsOn = start,
        EndsOn = end,
        Status = PayPeriodStatus.Open,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    private static async Task EnsureTimeAsync(
        ISupportStore store,
        string tenantId,
        IReadOnlyList<StaffAccessRecord> people,
        DateOnly start,
        DateOnly end,
        int targetHours)
    {
        var startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = end.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);
        var existing = await store.ListTimeEntriesAsync(tenantId, null, TimeEntryStatus.Approved);
        var targetMinutes = targetHours * 60;
        var spanDays = Math.Max(1, end.DayNumber - start.DayNumber);
        var chunks = Math.Clamp(targetHours / 8, 1, Math.Min(10, spanDays));
        var minutesEach = targetMinutes / chunks;
        var remainder = targetMinutes - minutesEach * chunks;

        foreach (var person in people)
        {
            var userId = person.UserId ?? person.Id;
            var already = existing
                .Where(e => e.UserId == userId || string.Equals(e.UserName, StaffLabel(person), StringComparison.OrdinalIgnoreCase))
                .Where(e =>
                {
                    var at = e.ClockIn ?? e.CreatedAt;
                    return at >= startUtc && at <= endUtc;
                })
                .Sum(e => e.Minutes ?? 0);
            if (already >= targetMinutes)
                continue;

            var name = StaffLabel(person);
            for (var i = 0; i < chunks; i++)
            {
                var dayOffset = Math.Min(spanDays - 1, i * Math.Max(1, spanDays / chunks));
                var worked = start.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc).AddDays(dayOffset);
                var minutes = minutesEach + (i == chunks - 1 ? remainder : 0);
                await store.InsertTimeEntryAsync(new TimeEntryEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = userId,
                    UserName = name,
                    EntryType = TimeEntryType.Manual,
                    ClockIn = worked,
                    ClockOut = worked.AddMinutes(minutes),
                    Minutes = minutes,
                    Notes = "seed:payroll",
                    Status = TimeEntryStatus.Approved,
                    CreatedAt = worked
                });
            }
        }
    }

    private static List<StaffAccessRecord> DistinctStaff(IReadOnlyList<StaffAccessRecord> roster, string tenantId)
    {
        var distinct = roster
            .Where(s => s.Active && StaffStatuses.IsLoginAllowed(s.Status))
            .Where(s => BelongsToTenant(s, tenantId))
            .GroupBy(s => s.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(s => s.UserId.HasValue).First())
            .ToList();

        var hasCom = distinct.Any(s => s.Email.EndsWith("@sominnercore.com", StringComparison.OrdinalIgnoreCase));
        if (hasCom)
        {
            distinct = distinct
                .Where(s => !s.Email.EndsWith("@sominnercore.local", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return distinct;
    }

    internal static bool BelongsToTenant(StaffAccessRecord staff, string tenantId)
    {
        if (staff.ProductSlugs.Any(p => string.Equals(p, tenantId, StringComparison.OrdinalIgnoreCase)))
            return true;
        var isKobNeti = string.Equals(tenantId, "kobneti", StringComparison.OrdinalIgnoreCase);
        return isKobNeti && staff.ProductSlugs.Count == 0;
    }

    private static string StaffLabel(StaffAccessRecord staff)
    {
        if (!string.IsNullOrWhiteSpace(staff.DisplayName))
            return staff.DisplayName.Trim();
        var local = staff.Email.Split('@')[0].Replace('.', ' ').Replace('_', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(local);
    }

    private static decimal RateForRole(string role) => role.ToLowerInvariant() switch
    {
        StaffRoles.Admin => 45m,
        StaffRoles.Manager => 42m,
        StaffRoles.Engineer => 38m,
        _ => 30m
    };

    private static (DateOnly Start, DateOnly End) CurrentHalf(DateTime utc)
    {
        var day = DateOnly.FromDateTime(utc);
        if (day.Day <= 15)
            return (new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, 15));
        var last = DateTime.DaysInMonth(day.Year, day.Month);
        return (new DateOnly(day.Year, day.Month, 16), new DateOnly(day.Year, day.Month, last));
    }

    private static (DateOnly Start, DateOnly End) PreviousHalf(DateOnly currentStart)
    {
        if (currentStart.Day == 16)
            return (new DateOnly(currentStart.Year, currentStart.Month, 1), new DateOnly(currentStart.Year, currentStart.Month, 15));
        var prior = currentStart.AddDays(-1);
        return (new DateOnly(prior.Year, prior.Month, 16), prior);
    }

    private static string FormatRange(DateOnly start, DateOnly end) =>
        start.Year == end.Year
            ? $"{start:MMM d} – {end:MMM d, yyyy}"
            : $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
}
