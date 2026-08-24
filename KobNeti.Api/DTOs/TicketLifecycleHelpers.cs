namespace KobNeti.Api.DTOs;

public static class TicketTags
{
    public static List<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? Serialize(IEnumerable<string>? tags)
    {
        var list = (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }
}

public static class TicketSla
{
    /// <summary>First-response SLA minutes by product support tier.</summary>
    public static int FirstResponseMinutes(string? supportTier) =>
        (supportTier ?? "standard").Trim().ToLowerInvariant() switch
        {
            "enterprise" => 60,
            "priority" => 240,
            _ => 1440 // standard: 24h
        };

    public static int ResolveMinutes(string? supportTier) =>
        (supportTier ?? "standard").Trim().ToLowerInvariant() switch
        {
            "enterprise" => 24 * 60,
            "priority" => 72 * 60,
            _ => 7 * 24 * 60
        };
}
