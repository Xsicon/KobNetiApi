using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Tests;

public class TenantIsolationTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public TenantIsolationTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TenantB_Cannot_Read_TenantA_ChatSession()
    {
        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.TenantASecret);
        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.TenantBSecret);

        using var clientA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var send = await clientA.PostAsJsonAsync("api/Chat/send", new SendMessageRequest
        {
            GuestName = "Alice",
            GuestEmail = "alice@example.com",
            Message = "Hello from A"
        });
        send.EnsureSuccessStatusCode();
        var sendBody = await send.Content.ReadFromJsonAsync<Response<ChatMessageDTO>>(SupportApiFactory.JsonOptions);
        Assert.NotNull(sendBody?.Data);
        var sessionId = sendBody!.Data!.SessionId;

        using var clientB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var messages = await clientB.GetAsync($"api/Chat/messages/{sessionId}");
        var messagesBody = await messages.Content.ReadFromJsonAsync<Response<List<ChatMessageDTO>>>(SupportApiFactory.JsonOptions);
        Assert.True(messages.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.OK);
        Assert.True(messagesBody is null || !messagesBody.Success || messagesBody.Data is null || messagesBody.Data.Count == 0
                    || messagesBody.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));

        using var adminB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var session = await adminB.GetAsync($"api/Chat/session/{sessionId}");
        var sessionBody = await session.Content.ReadFromJsonAsync<Response<ChatSessionDTO>>(SupportApiFactory.JsonOptions);
        Assert.False(sessionBody?.Success == true && sessionBody.Data?.Id == sessionId);

        using var adminA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var ok = await adminA.GetAsync($"api/Chat/session/{sessionId}");
        ok.EnsureSuccessStatusCode();
        var okBody = await ok.Content.ReadFromJsonAsync<Response<ChatSessionDTO>>(SupportApiFactory.JsonOptions);
        Assert.True(okBody?.Success);
        Assert.Equal(sessionId, okBody!.Data!.Id);
    }

    [Fact]
    public async Task TenantB_Cannot_Read_TenantA_Ticket()
    {
        using var clientA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var create = await clientA.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Alice",
            Email = "alice@example.com",
            Category = "Orders",
            Subject = "Where is my order?",
            Message = "Need help"
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions);
        Assert.NotNull(created?.Data);
        var ticketId = created!.Data!.Id;

        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.TenantBSecret);
        using var clientB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var get = await clientB.GetAsync($"api/Help/admin/tickets/{ticketId}");
        var body = await get.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions);
        Assert.False(body?.Success == true);

        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.TenantASecret);
        using var clientAAdmin = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var ok = await clientAAdmin.GetAsync($"api/Help/admin/tickets/{ticketId}");
        ok.EnsureSuccessStatusCode();
        var okBody = await ok.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions);
        Assert.True(okBody?.Success);
        Assert.Equal(ticketId, okBody!.Data!.Id);
    }

    [Fact]
    public async Task Public_TenantA_Cannot_List_TenantB_Published_Articles()
    {
        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.TenantBSecret);
        using var adminB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var create = await adminB.PostAsJsonAsync("api/Help/admin/articles", new SaveHelpArticleDTO
        {
            Title = "Secret B Article",
            Category = "Orders",
            Content = "Only for B",
            Status = "published"
        });
        create.EnsureSuccessStatusCode();

        using var publicA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var list = await publicA.GetAsync("api/Help/articles");
        list.EnsureSuccessStatusCode();
        var body = await list.Content.ReadFromJsonAsync<Response<PaginatedResponse<HelpArticleDTO>>>(SupportApiFactory.JsonOptions);
        Assert.True(body?.Success);
        Assert.DoesNotContain(body!.Data!.Data, a => a.Title == "Secret B Article");

        using var publicB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey);
        var listB = await publicB.GetAsync("api/Help/articles");
        listB.EnsureSuccessStatusCode();
        var bodyB = await listB.Content.ReadFromJsonAsync<Response<PaginatedResponse<HelpArticleDTO>>>(SupportApiFactory.JsonOptions);
        Assert.Contains(bodyB!.Data!.Data, a => a.Title == "Secret B Article");
    }

    [Fact]
    public async Task Missing_Tenant_Key_Is_Rejected()
    {
        using var client = _factory.CreateClient();
        var res = await client.GetAsync("api/Help/articles");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Agent_Scoped_To_Muuqwear_Cannot_Access_Salguri()
    {
        var scoped = SupportApiFactory.CreateAgentToken(
            SupportApiFactory.CoreSecret,
            AdminRoles.SupportTeam,
            "muuqwear");

        using var client = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, scoped);
        var res = await client.GetAsync("api/Support/counts");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
