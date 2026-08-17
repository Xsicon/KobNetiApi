using System.Net;
using System.Net.Http.Json;
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