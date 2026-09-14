using KobNeti.Api.Email;

namespace KobNeti.Api.Tests;

public class SupabaseAuthLinkTests
{
    [Fact]
    public void Prefers_hashed_token_on_kobneti_reset_page()
    {
        const string json = """
            {
              "action_link": "https://yvooccmbtokzfnqibyig.supabase.co/auth/v1/verify?token=pkce_abc&type=magiclink&redirect_to=https://salguri.abdirahmaan.dev/reset-password",
              "properties": {
                "action_link": "https://yvooccmbtokzfnqibyig.supabase.co/auth/v1/verify?token=pkce_abc&type=invite&redirect_to=https://salguri.abdirahmaan.dev/reset-password",
                "hashed_token": "hashed-invite-token",
                "verification_type": "invite"
              }
            }
            """;

        var link = SupabaseAuthLink.FromGenerateLinkJson(json, "https://localhost:5001/admin/reset-password");

        Assert.Equal(
            "https://localhost:5001/admin/reset-password?token_hash=hashed-invite-token&type=invite",
            link);
        Assert.DoesNotContain("salguri", link, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("supabase.co/auth/v1/verify", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rewrites_action_link_redirect_when_hashed_token_missing()
    {
        const string json = """
            {
              "action_link": "https://proj.supabase.co/auth/v1/verify?token=abc&type=recovery&redirect_to=https://salguri.abdirahmaan.dev/reset-password"
            }
            """;

        var link = SupabaseAuthLink.FromGenerateLinkJson(json, "https://ops.kobneti.com/admin/reset-password");
        Assert.NotNull(link);
        var decoded = Uri.UnescapeDataString(link!);
        Assert.Contains("ops.kobneti.com/admin/reset-password", decoded);
        Assert.DoesNotContain("salguri", decoded, StringComparison.OrdinalIgnoreCase);
    }
}
