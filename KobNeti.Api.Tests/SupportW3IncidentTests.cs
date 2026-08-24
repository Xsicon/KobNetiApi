using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW3IncidentTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW3IncidentTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Escalate_ticket_creates_incident_with_timeline_and_postmortem()
    {
        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Eve",
            Email = "eve@example.com",
            Category = "outage",
            Subject = "Checkout down",
            Message = "Payments failing"
        });
        submit.EnsureSuccessStatusCode();
        var ticket = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var escalate = await agent.PostAsJsonAsync(
            $"api/Incidents/from-ticket/{ticket.Id}",
            new EscalateTicketDTO { Severity = IncidentSeverity.Sev1, CommanderName = "Eve Lead" });
        Assert.Equal(HttpStatusCode.OK, escalate.StatusCode);
        var incident = (await escalate.Content.ReadFromJsonAsync<Response<IncidentDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(ticket.Id, incident.SourceTicketId);
        Assert.Equal(IncidentSeverity.Sev1, incident.Severity);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Contains(incident.Timeline, e => e.EventType == "created");
        Assert.Contains(incident.Timeline, e => e.EventType == "escalated_from_ticket");

        var patch = await agent.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"api/Incidents/{incident.Id}")
        {
            Content = JsonContent.Create(new UpdateIncidentDTO
            {
                Status = IncidentStatus.Investigating,
                PostmortemNotes = "Root cause: bad deploy"
            })
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = (await patch.Content.ReadFromJsonAsync<Response<IncidentDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(IncidentStatus.Investigating, updated.Status);
        Assert.Equal("Root cause: bad deploy", updated.PostmortemNotes);
        Assert.Contains(updated.Timeline, e => e.EventType == "postmortem_updated");

        var list = await agent.GetFromJsonAsync<Response<List<IncidentDTO>>>(
            "api/Incidents", SupportApiFactory.JsonOptions);
        Assert.Contains(list!.Data!, i => i.Id == incident.Id);
    }

    [Fact]
    public async Task TenantB_cannot_read_TenantA_incident()
    {
        var publicA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicA.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Iso",
            Email = "iso@example.com",
            Category = "security",
            Subject = "Leak",
            Message = "Isolate me"
        });
        var ticket = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var escalate = await agentA.PostAsJsonAsync(
            $"api/Incidents/from-ticket/{ticket.Id}",
            new EscalateTicketDTO());
        escalate.EnsureSuccessStatusCode();
        var incident = (await escalate.Content.ReadFromJsonAsync<Response<IncidentDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var getB = await agentB.GetAsync($"api/Incidents/{incident.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, getB.StatusCode);
        var bodyB = await getB.Content.ReadFromJsonAsync<Response<IncidentDTO>>(SupportApiFactory.JsonOptions);
        Assert.False(bodyB!.Success);

        var listB = await agentB.GetFromJsonAsync<Response<List<IncidentDTO>>>(
            "api/Incidents", SupportApiFactory.JsonOptions);
        Assert.DoesNotContain(listB!.Data ?? [], i => i.Id == incident.Id);
    }
}
