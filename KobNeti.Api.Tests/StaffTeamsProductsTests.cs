using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class StaffTeamsProductsTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public StaffTeamsProductsTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Platform_admin_can_invite_list_and_deactivate_staff()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var invite = await client.PostAsJsonAsync("api/Staff/invite", new InviteStaffDTO
        {
            Email = "agent.w1@test.local",
            DisplayName = "W1 Agent",
            Role = StaffRoles.Support,
            ProductSlugs = ["muuqwear"]
        });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var invited = await invite.Content.ReadFromJsonAsync<Response<StaffMemberDTO>>(SupportApiFactory.JsonOptions);
        Assert.NotNull(invited?.Data);
        Assert.Equal("agent.w1@test.local", invited!.Data!.Email);

        var list = await client.GetFromJsonAsync<Response<List<StaffMemberDTO>>>("api/Staff", SupportApiFactory.JsonOptions);
        Assert.Contains(list!.Data!, s => s.Id == invited.Data.Id);

        var deactivate = await client.PostAsync($"api/Staff/{invited.Data.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<Response<StaffMemberDTO>>(SupportApiFactory.JsonOptions);
        Assert.False(deactivated!.Data!.Active);
        Assert.Equal("deactivated", deactivated.Data.Status);
    }

    [Fact]
    public async Task Platform_admin_can_suspend_staff_and_read_stats()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var invite = await client.PostAsJsonAsync("api/Staff/invite", new InviteStaffDTO
        {
            Email = "agent.suspend@test.local",
            DisplayName = "Suspend Me",
            Role = StaffRoles.Engineer,
            Roles = [StaffRoles.Engineer, StaffRoles.Manager],
            ProductSlugs = ["muuqwear"]
        });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var invited = (await invite.Content.ReadFromJsonAsync<Response<StaffMemberDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Contains("manager", invited.Roles);
        Assert.Equal("active", invited.Status);

        var suspend = await client.PostAsJsonAsync($"api/Staff/{invited.Id}/status", new UpdateStaffStatusDTO { Status = "suspended" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspended = (await suspend.Content.ReadFromJsonAsync<Response<StaffMemberDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal("suspended", suspended.Status);
        Assert.False(suspended.Active);

        var stats = await client.GetFromJsonAsync<Response<StaffStatsDTO>>("api/Staff/stats", SupportApiFactory.JsonOptions);
        Assert.True(stats!.Data!.TotalUsers >= 1);
        Assert.True(stats.Data.PendingInvites >= 1);
        Assert.True(stats.Data.SuspendedCount >= 1);

        var invites = await client.GetFromJsonAsync<Response<List<StaffInviteDTO>>>("api/Staff/invites", SupportApiFactory.JsonOptions);
        var openInvite = Assert.Single(invites!.Data!, i => i.Email == "agent.suspend@test.local");
        Assert.False(string.IsNullOrWhiteSpace(openInvite.Status));
        Assert.True(openInvite.Status is "pending" or "expiring_soon");

        var resend = await client.PostAsJsonAsync($"api/Staff/invites/{openInvite.Id}/resend", new ResendStaffInviteDTO());
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var cancel = await client.PostAsync($"api/Staff/invites/{openInvite.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var afterCancel = await client.GetFromJsonAsync<Response<List<StaffInviteDTO>>>("api/Staff/invites", SupportApiFactory.JsonOptions);
        Assert.Equal("revoked", afterCancel!.Data!.First(i => i.Id == openInvite.Id).Status);

        var csv = await client.GetAsync("api/Staff/export");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Logging_in_marks_open_invite_accepted()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);
        const string email = "accepted.invite@test.local";

        var invite = await client.PostAsJsonAsync("api/Staff/invite", new InviteStaffDTO
        {
            Email = email,
            Role = StaffRoles.Admin,
            ProductSlugs = ["muuqwear"]
        });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffDirectory>();
        await staff.TouchLastActiveAsync("Accepted.Invite@test.local", Guid.NewGuid());

        var invites = await client.GetFromJsonAsync<Response<List<StaffInviteDTO>>>("api/Staff/invites", SupportApiFactory.JsonOptions);
        Assert.Equal("accepted", invites!.Data!.First(i => i.Email == email).Status);
    }

    [Fact]
    public async Task Support_role_cannot_manage_staff()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Support, "muuqwear");
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);
        var res = await client.GetAsync("api/Staff");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_create_team_and_add_member()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var invite = await client.PostAsJsonAsync("api/Staff/invite", new InviteStaffDTO
        {
            Email = "team.member@test.local",
            Role = StaffRoles.Support,
            ProductSlugs = ["muuqwear"]
        });
        var staff = (await invite.Content.ReadFromJsonAsync<Response<StaffMemberDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var create = await client.PostAsJsonAsync("api/Teams", new CreateTeamDTO
        {
            Name = "MuuqWear L1",
            ProductSlug = "muuqwear"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var team = (await create.Content.ReadFromJsonAsync<Response<TeamDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var add = await client.PostAsJsonAsync($"api/Teams/{team.Id}/members", new AddTeamMemberDTO
        {
            StaffId = staff.Id,
            MemberRole = "lead"
        });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var withMember = (await add.Content.ReadFromJsonAsync<Response<TeamDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Contains(withMember.Members, m => m.StaffId == staff.Id && m.MemberRole == "lead");
    }

    [Fact]
    public async Task Platform_admin_can_rotate_embed_key()
    {
        // Own factory — rotation mutates the in-memory public key.
        await using var factory = new SupportApiFactory();
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var client = factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var before = await client.GetFromJsonAsync<Response<List<ProductDTO>>>("api/products", SupportApiFactory.JsonOptions);
        var product = before!.Data!.First(p => p.TenantId == "muuqwear");
        var oldKey = product.PublicKey;

        var rotate = await client.PostAsync("api/products/muuqwear/rotate-key", null);
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
        var body = await rotate.Content.ReadFromJsonAsync<Response<RotateEmbedKeyDTO>>(SupportApiFactory.JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body!.Data!.PublicKey));
        Assert.NotEqual(oldKey, body.Data.PublicKey);
        Assert.Contains(body.Data.PublicKey, body.Data.WidgetSnippet);

        var newClient = factory.CreateTenantClient(body.Data.PublicKey, token);
        var tenants = await newClient.GetAsync("api/Support/tenants");
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
    }
}