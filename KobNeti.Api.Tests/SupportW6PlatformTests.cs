using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW6PlatformTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW6PlatformTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Audit_notifications_calendar_files_and_integrations()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Pat",
            Email = "pat@example.com",
            Category = "orders",
            Subject = "Audit me",
            Message = "Status change"
        });
        var ticket = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var status = await agent.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"api/Help/admin/tickets/{ticket.Id}/status")
        {
            Content = JsonContent.Create(new { status = TicketStatus.InProgress })
        });
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        var assign = await agent.PostAsync($"api/Help/admin/tickets/{ticket.Id}/assign-me", null);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var audit = await agent.GetFromJsonAsync<Response<List<AuditEventDTO>>>(
            "api/Audit?action=ticket.status", SupportApiFactory.JsonOptions);
        Assert.True(audit!.Success);
        Assert.Contains(audit.Data!, e => e.Action == AuditActions.TicketStatus);

        var notes = await agent.GetFromJsonAsync<Response<List<NotificationDTO>>>(
            "api/Notifications", SupportApiFactory.JsonOptions);
        Assert.True(notes!.Success);
        Assert.Contains(notes.Data!, n => n.Title.Contains("assigned", StringComparison.OrdinalIgnoreCase)
                                          || n.Title.Contains("Ticket", StringComparison.OrdinalIgnoreCase));

        var prefs = await agent.PutAsJsonAsync("api/Notifications/preferences", new NotificationPrefsDTO
        {
            AssignEnabled = true,
            ApprovalEnabled = false,
            EscalationEnabled = true,
            ReminderEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, prefs.StatusCode);

        var cal = await agent.PostAsJsonAsync("api/Calendar/events", new CreateCalendarEventDTO
        {
            Title = "Standup",
            StartsAt = DateTime.UtcNow.AddHours(2),
            EventType = "meeting"
        });
        Assert.Equal(HttpStatusCode.OK, cal.StatusCode);

        var remind = await agent.PostAsync("api/Calendar/reminders?withinHours=48", null);
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        var remindBody = await remind.Content.ReadFromJsonAsync<Response<int>>(SupportApiFactory.JsonOptions);
        Assert.True(remindBody!.Data >= 1);

        var file = await agent.PostAsJsonAsync("api/OpsFiles", new CreateOpsFileDTO
        {
            FileName = "spec.md",
            FolderPath = "/docs/"
        });
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        var files = await agent.GetFromJsonAsync<Response<List<OpsFileDTO>>>("api/OpsFiles", SupportApiFactory.JsonOptions);
        Assert.Contains(files!.Data!, f => f.FileName == "spec.md");

        var connect = await agent.PostAsJsonAsync("api/Integrations/connect", new ConnectIntegrationDTO
        {
            Provider = "github",
            Secrets = new Dictionary<string, string> { ["pat"] = "ghp_test_secret" }
        });
        Assert.Equal(HttpStatusCode.OK, connect.StatusCode);
        var connected = (await connect.Content.ReadFromJsonAsync<Response<IntegrationDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal("connected", connected.Status);
        Assert.True(connected.HasSecrets);

        var disconnect = await agent.PostAsync("api/Integrations/github/disconnect", null);
        Assert.Equal(HttpStatusCode.OK, disconnect.StatusCode);
    }

    [Fact]
    public async Task TenantB_cannot_see_TenantA_audit_or_files()
    {
        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        await agentA.PostAsJsonAsync("api/OpsFiles", new CreateOpsFileDTO { FileName = "secret-a.txt", FolderPath = "/" });

        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var filesB = await agentB.GetFromJsonAsync<Response<List<OpsFileDTO>>>("api/OpsFiles", SupportApiFactory.JsonOptions);
        Assert.DoesNotContain(filesB!.Data ?? [], f => f.FileName == "secret-a.txt");
    }
}
