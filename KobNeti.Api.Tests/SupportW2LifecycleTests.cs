using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW2LifecycleTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW2LifecycleTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ticket_gets_sla_tags_timeline_and_eng_task_link()
    {
        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Casey",
            Email = "casey@example.com",
            Category = "orders",
            Subject = "Late shipment",
            Message = "Still waiting"
        });
        submit.EnsureSuccessStatusCode();
        var created = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.NotNull(created.SlaFirstResponseMinutes);
        Assert.NotNull(created.FirstResponseDueAt);
        Assert.NotNull(created.ResolveDueAt);

        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);
        var engId = Guid.NewGuid();
        var patch = await agent.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"api/Help/admin/tickets/{created.Id}")
        {
            Content = JsonContent.Create(new UpdateTicketDTO
            {
                Tags = ["shipping", "vip"],
                EngTaskId = engId,
                Status = TicketStatus.InProgress
            })
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var detail = await agent.GetFromJsonAsync<Response<SupportTicketDTO>>(
            $"api/Help/admin/tickets/{created.Id}", SupportApiFactory.JsonOptions);
        Assert.Contains("shipping", detail!.Data!.Tags);
        Assert.Contains("vip", detail.Data.Tags);
        Assert.Equal(engId, detail.Data.EngTaskId);
        Assert.Equal(TicketStatus.InProgress, detail.Data.Status);
        Assert.NotEmpty(detail.Data.Timeline);
        Assert.Contains(detail.Data.Timeline, e => e.EventType is "created" or "updated" or "status_changed");
    }

    [Fact]
    public async Task Suggest_articles_filters_by_ticket_category()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var article = await agent.PostAsJsonAsync("api/Help/admin/articles", new SaveHelpArticleDTO
        {
            Title = "How shipping works",
            Category = "orders",
            Content = "Track your package",
            Status = HelpArticleStatus.Published
        });
        article.EnsureSuccessStatusCode();

        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Dana",
            Email = "dana@example.com",
            Category = "orders",
            Subject = "Shipping question",
            Message = "Help"
        });
        var ticket = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var suggestions = await agent.GetFromJsonAsync<Response<List<HelpArticleDTO>>>(
            $"api/Help/admin/tickets/{ticket.Id}/suggestions", SupportApiFactory.JsonOptions);
        Assert.True(suggestions!.Success);
        Assert.Contains(suggestions.Data!, a => a.Title.Contains("shipping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Macros_list_endpoint_works_for_agent()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var create = await agent.PostAsJsonAsync("api/Support/macros", new SaveMacroDTO
        {
            Title = "Greeting",
            Body = "Thanks for contacting us.",
            Category = "chat"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await agent.GetFromJsonAsync<Response<List<SupportMacroDTO>>>(
            "api/Support/macros", SupportApiFactory.JsonOptions);
        Assert.Contains(list!.Data!, m => m.Title == "Greeting");
    }
}
