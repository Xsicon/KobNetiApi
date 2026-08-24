using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW4EngineeringTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW4EngineeringTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Task_board_milestone_calendar_and_ticket_link()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        var submit = await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Dev",
            Email = "dev@example.com",
            Category = "bug",
            Subject = "Checkout 500",
            Message = "Null ref"
        });
        var ticket = (await submit.Content.ReadFromJsonAsync<Response<SupportTicketDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var ms = await agent.PostAsJsonAsync("api/Milestones", new CreateMilestoneDTO
        {
            Title = "Q1 Hardening",
            TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            Status = MilestoneStatus.Active
        });
        Assert.Equal(HttpStatusCode.OK, ms.StatusCode);
        var milestone = (await ms.Content.ReadFromJsonAsync<Response<MilestoneDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.NotNull(milestone.CalendarEventId);

        var cal = await agent.GetFromJsonAsync<Response<List<CalendarEventDTO>>>(
            "api/Calendar/events", SupportApiFactory.JsonOptions);
        Assert.Contains(cal!.Data!, e => e.SourceEntityId == milestone.Id);

        var createTask = await agent.PostAsJsonAsync("api/EngTasks", new CreateEngTaskDTO
        {
            Title = "Fix checkout null ref",
            TaskType = EngTaskType.Bug,
            Priority = EngTaskPriority.High,
            Status = EngTaskStatus.Ready,
            TicketId = ticket.Id,
            MilestoneId = milestone.Id,
            GithubPrUrl = "https://github.com/example/repo/pull/1"
        });
        Assert.Equal(HttpStatusCode.OK, createTask.StatusCode);
        var task = (await createTask.Content.ReadFromJsonAsync<Response<EngTaskDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(ticket.Id, task.TicketId);
        Assert.Equal(milestone.Id, task.MilestoneId);
        Assert.Equal("https://github.com/example/repo/pull/1", task.GithubPrUrl);

        var ticketDetail = await agent.GetFromJsonAsync<Response<SupportTicketDTO>>(
            $"api/Help/admin/tickets/{ticket.Id}", SupportApiFactory.JsonOptions);
        Assert.Equal(task.Id, ticketDetail!.Data!.EngTaskId);

        var board = await agent.GetFromJsonAsync<Response<List<EngTaskDTO>>>(
            "api/EngTasks?status=ready", SupportApiFactory.JsonOptions);
        Assert.Contains(board!.Data!, t => t.Id == task.Id);
    }

    [Fact]
    public async Task TenantB_cannot_see_TenantA_tasks_or_milestones()
    {
        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentA = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var create = await agentA.PostAsJsonAsync("api/EngTasks", new CreateEngTaskDTO
        {
            Title = "Secret A task"
        });
        create.EnsureSuccessStatusCode();
        var task = (await create.Content.ReadFromJsonAsync<Response<EngTaskDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var ms = await agentA.PostAsJsonAsync("api/Milestones", new CreateMilestoneDTO { Title = "Secret A ms" });
        ms.EnsureSuccessStatusCode();
        var milestone = (await ms.Content.ReadFromJsonAsync<Response<MilestoneDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agentB = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);

        var getTask = await agentB.GetAsync($"api/EngTasks/{task.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, getTask.StatusCode);

        var listTasks = await agentB.GetFromJsonAsync<Response<List<EngTaskDTO>>>(
            "api/EngTasks", SupportApiFactory.JsonOptions);
        Assert.DoesNotContain(listTasks!.Data ?? [], t => t.Id == task.Id);

        var getMs = await agentB.GetAsync($"api/Milestones/{milestone.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, getMs.StatusCode);
    }

    [Fact]
    public async Task Github_repo_url_can_be_set_and_cache_starts_empty()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var patch = await agent.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "api/products/muuqwear/github-repo")
        {
            Content = JsonContent.Create(new UpdateProductGithubRepoDTO
            {
                GithubRepoUrl = "https://github.com/Xsicon/KobNeti"
            })
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var cache = await agent.GetFromJsonAsync<Response<GithubCacheDTO>>(
            "api/Github", SupportApiFactory.JsonOptions);
        Assert.True(cache!.Success);
        Assert.Equal("https://github.com/Xsicon/KobNeti", cache.Data!.RepoUrl);
        Assert.Empty(cache.Data.Pulls);

        Assert.True(Services.GithubReadService.TryParseRepo(
            "https://github.com/Xsicon/KobNetiApi.git", out var owner, out var repo));
        Assert.Equal("Xsicon", owner);
        Assert.Equal("KobNetiApi", repo);
    }
}
