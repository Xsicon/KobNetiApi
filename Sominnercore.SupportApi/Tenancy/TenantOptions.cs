namespace Sominnercore.SupportApi.Tenancy;

public class SupportOptions
{
    public const string SectionName = "Support";

    public bool UseInMemoryStore { get; set; }
    public string CoreAgentJwtSecret { get; set; } = "dev-core-agent-secret-change-me-32chars!";
    public string[] CoreAdminEmails { get; set; } = [];
    public Dictionary<string, TenantConfig> Tenants { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class TenantConfig
{
    public string DisplayName { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public bool Enabled { get; set; }

    /// <summary>
    /// When set (e.g. http://localhost:5243/), Chat is proxied to this storefront API
    /// so Support Hub can show live MuuqWear chats before Core owns the data.
    /// </summary>
    public string UpstreamApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public Help Center URL for this brand (e.g. storefront /help).
    /// </summary>
    public string PublicHelpCenterUrl { get; set; } = string.Empty;
}

public class TenantContext
{
    public string TenantId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string JwtSecret { get; init; } = string.Empty;
    public string UpstreamApiBaseUrl { get; init; } = string.Empty;
    public string PublicHelpCenterUrl { get; init; } = string.Empty;
}

public interface ITenantContextAccessor
{
    TenantContext? Current { get; set; }
}

public class TenantContextAccessor : ITenantContextAccessor
{
    public TenantContext? Current { get; set; }
}
