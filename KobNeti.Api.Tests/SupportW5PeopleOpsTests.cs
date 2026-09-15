using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW5PeopleOpsTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW5PeopleOpsTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Clock_manual_edit_approval_and_payroll_export()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var clockIn = await agent.PostAsJsonAsync("api/TimeEntries/clock-in", new ClockInDTO());
        Assert.Equal(HttpStatusCode.OK, clockIn.StatusCode);

        var clockOut = await agent.PostAsJsonAsync("api/TimeEntries/clock-out", new { });
        Assert.Equal(HttpStatusCode.OK, clockOut.StatusCode);
        var session = (await clockOut.Content.ReadFromJsonAsync<Response<TimeEntryDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(TimeEntryStatus.Approved, session.Status);
        Assert.True(session.Minutes >= 1);

        var manual = await agent.PostAsJsonAsync("api/TimeEntries/manual", new ManualTimeDTO
        {
            Minutes = 120,
            Notes = "Ticket work"
        });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode);
        var manualEntry = (await manual.Content.ReadFromJsonAsync<Response<TimeEntryDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var edit = await agent.PostAsJsonAsync("api/TimeEntries/request-edit", new RequestTimeEditDTO
        {
            EntryId = manualEntry.Id,
            Minutes = 150,
            Reason = "Forgot 30 mins"
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var approval = (await edit.Content.ReadFromJsonAsync<Response<ApprovalRequestDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(ApprovalRequestType.TimeEdit, approval.RequestType);

        var decide = await agent.PostAsJsonAsync($"api/Approvals/{approval.Id}/decide", new DecideApprovalDTO { Approve = true });
        Assert.Equal(HttpStatusCode.OK, decide.StatusCode);

        var entries = await agent.GetFromJsonAsync<Response<List<TimeEntryDTO>>>(
            "api/TimeEntries", SupportApiFactory.JsonOptions);
        Assert.Contains(entries!.Data!, e => e.Id == manualEntry.Id && e.Status == TimeEntryStatus.Superseded);
        Assert.Contains(entries.Data!, e => e.SupersedesId == manualEntry.Id && e.Status == TimeEntryStatus.Approved && e.Minutes == 150);

        var rate = await agent.PostAsJsonAsync("api/Payroll/rates", new SavePayRateDTO
        {
            Role = "admin",
            HourlyRate = 60m
        });
        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodRes = await agent.PostAsJsonAsync("api/Payroll/periods", new CreatePayPeriodDTO
        {
            Label = "Test period",
            StartsOn = today.AddDays(-1),
            EndsOn = today.AddDays(1)
        });
        Assert.Equal(HttpStatusCode.OK, periodRes.StatusCode);
        var period = (await periodRes.Content.ReadFromJsonAsync<Response<PayPeriodDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var calc = await agent.PostAsync($"api/Payroll/periods/{period.Id}/calculate", null);
        Assert.Equal(HttpStatusCode.OK, calc.StatusCode);
        var calculated = (await calc.Content.ReadFromJsonAsync<Response<PayPeriodDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(PayPeriodStatus.Calculated, calculated.Status);
        Assert.True(calculated.TotalMinutes >= 150);
        Assert.True(calculated.TotalAmount > 0);

        var submit = await agent.PostAsync($"api/Payroll/periods/{period.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var pendingPayroll = (await submit.Content.ReadFromJsonAsync<Response<PayPeriodDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(PayPeriodStatus.PendingApproval, pendingPayroll.Status);
        Assert.NotNull(pendingPayroll.ApprovalId);

        var approvePayroll = await agent.PostAsJsonAsync(
            $"api/Approvals/{pendingPayroll.ApprovalId}/decide",
            new DecideApprovalDTO { Approve = true });
        Assert.Equal(HttpStatusCode.OK, approvePayroll.StatusCode);

        var export = await agent.GetAsync($"api/Payroll/periods/{period.Id}/export.csv");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("user_name", csv);
        Assert.Contains("minutes", csv);

        var pdfRes = await agent.GetAsync($"api/Payroll/periods/{period.Id}/export.pdf");
        Assert.Equal(HttpStatusCode.OK, pdfRes.StatusCode);
        Assert.Equal("application/pdf", pdfRes.Content.Headers.ContentType?.MediaType);
        var pdf = await pdfRes.Content.ReadAsByteArrayAsync();
        Assert.True(pdf.Length > 200);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Take(4).ToArray());
    }

    [Fact]
    public async Task TenantB_cannot_see_TenantA_time_entries()
    {
        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var add = await agentA.PostAsJsonAsync("api/TimeEntries/manual", new ManualTimeDTO { Minutes = 45 });
        add.EnsureSuccessStatusCode();
        var entry = (await add.Content.ReadFromJsonAsync<Response<TimeEntryDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);
        var listB = await agentB.GetFromJsonAsync<Response<List<TimeEntryDTO>>>(
            "api/TimeEntries", SupportApiFactory.JsonOptions);
        Assert.DoesNotContain(listB!.Data ?? [], e => e.Id == entry.Id);
    }
}
