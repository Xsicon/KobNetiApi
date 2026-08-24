using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW2IntakeTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW2IntakeTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_ticket_captures_page_url_and_account_id()
    {
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await client.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Ada",
            Email = "ada@example.com",
            Category = "orders",
            Subject = "Where is my order?",
            Message = "Need tracking",
            PageUrl = "https://shop.example/orders/1",
            AccountId = "acct_123"
        });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal(TicketStatus.New, body.Data!.Status);
        Assert.Equal("https://shop.example/orders/1", body.Data.PageUrl);
        Assert.Equal("acct_123", body.Data.AccountId);
    }

    [Fact]
    public async Task Agent_can_convert_chat_session_to_ticket()
    {
        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var send = await publicClient.PostAsJsonAsync("api/Chat/send", new SendMessageRequest
        {
            GuestName = "Bob",
            GuestEmail = "bob@example.com",
            Message = "I need a refund"
        });
        send.EnsureSuccessStatusCode();
        var msg = (await send.Content.ReadFromJsonAsync<Response<ChatMessageDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);
        var convert = await agent.PostAsJsonAsync(
            $"api/Chat/session/{msg.SessionId}/convert-to-ticket",
            new ConvertChatToTicketDTO { Category = "returns", Subject = "Refund request" });
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var ticket = (await convert.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(msg.SessionId, ticket.ChatSessionId);
        Assert.Contains("refund", ticket.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("bob@example.com", ticket.Email);
    }

    [Fact]
    public async Task Email_to_ticket_is_stubbed()
    {
        var client = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var res = await client.PostAsync("api/Help/email-to-ticket", null);
        Assert.Equal(HttpStatusCode.NotImplemented, res.StatusCode);
    }

    [Fact]
    public async Task Widget_script_is_served()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("widget/support.js");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var js = await res.Content.ReadAsStringAsync();
        Assert.Contains("api/Help/ticket", js);
    }
}
