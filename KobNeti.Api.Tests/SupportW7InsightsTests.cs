using System.Net;
using System.Net.Http.Json;
using KobNeti.Api.Auth;
using KobNeti.Api.DTOs;
using KobNeti.Api.Shared;
using KobNeti.Api.Staff;

namespace KobNeti.Api.Tests;

public class SupportW7InsightsTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public SupportW7InsightsTests(SupportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Overview_reports_help_im_and_assets()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var overview = await agent.GetFromJsonAsync<Response<OverviewDTO>>(
            "api/Overview", SupportApiFactory.JsonOptions);
        Assert.True(overview!.Success);
        Assert.Equal("muuqwear", overview.Data!.TenantId);

        var cross = await agent.GetFromJsonAsync<Response<CrossProductOverviewDTO>>(
            "api/Overview/cross-product", SupportApiFactory.JsonOptions);
        Assert.True(cross!.Success);
        Assert.True(cross.Data!.Products.Count >= 2);

        var publicClient = _factory.CreateTenantClient(SupportApiFactory.TenantAKey);
        await publicClient.PostAsJsonAsync("api/Help/ticket", new SubmitTicketDTO
        {
            Name = "Lee",
            Email = "lee@example.com",
            Category = "orders",
            Subject = "Report ticket",
            Message = "Need CSV"
        });

        var run = await agent.PostAsJsonAsync("api/Reports/run", new RunReportDTO
        {
            ReportType = "tickets",
            Label = "Tickets export"
        });
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        var runBody = (await run.Content.ReadFromJsonAsync<Response<ReportRunDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.True(runBody.RowCount >= 1);

        var csv = await agent.GetFromJsonAsync<Response<string>>(
            $"api/Reports/{runBody.Id}/csv", SupportApiFactory.JsonOptions);
        Assert.True(csv!.Success);
        Assert.Contains("subject", csv.Data!, StringComparison.OrdinalIgnoreCase);

        var help = await agent.GetFromJsonAsync<Response<List<PlatformHelpArticleDTO>>>(
            "api/PlatformHelp", SupportApiFactory.JsonOptions);
        Assert.True(help!.Success);
        Assert.Contains(help.Data!, a => a.Slug == "getting-started");

        var channelRes = await agent.PostAsJsonAsync("api/InternalChat/channels", new CreateImChannelDTO
        {
            Name = "ops-general",
            ChannelType = "channel"
        });
        Assert.Equal(HttpStatusCode.OK, channelRes.StatusCode);
        var channel = (await channelRes.Content.ReadFromJsonAsync<Response<ImChannelDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var msgRes = await agent.PostAsJsonAsync($"api/InternalChat/channels/{channel.Id}/messages",
            new SendImMessageDTO { Body = "Standup in 5" });
        Assert.Equal(HttpStatusCode.OK, msgRes.StatusCode);

        var msgs = await agent.GetFromJsonAsync<Response<List<ImMessageDTO>>>(
            $"api/InternalChat/channels/{channel.Id}/messages", SupportApiFactory.JsonOptions);
        Assert.Contains(msgs!.Data!, m => m.Body == "Standup in 5");

        var assetRes = await agent.PostAsJsonAsync("api/Assets", new SaveAssetDTO
        {
            Name = "MacBook",
            AssetType = "hardware",
            RenewalDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        });
        Assert.Equal(HttpStatusCode.OK, assetRes.StatusCode);
        var asset = (await assetRes.Content.ReadFromJsonAsync<Response<AssetDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var assign = await agent.PostAsJsonAsync($"api/Assets/{asset.Id}/assign", new AssignAssetDTO
        {
            UserName = "Test Agent"
        });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var remind = await agent.PostAsync("api/Assets/renewal-reminders?withinDays=30", null);
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        var remindBody = await remind.Content.ReadFromJsonAsync<Response<int>>(SupportApiFactory.JsonOptions);
        Assert.True(remindBody!.Data >= 1);
    }

    [Fact]
    public async Task InternalChat_creates_general_channel_topic_threads_and_dms()
    {
        var token = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var agent = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, token);

        var listed = await agent.GetFromJsonAsync<Response<List<ImChannelDTO>>>(
            "api/InternalChat/channels", SupportApiFactory.JsonOptions);
        Assert.True(listed!.Success);
        Assert.Contains(listed.Data!, c =>
            string.Equals(c.Name, "general", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.ChannelType, "channel", StringComparison.OrdinalIgnoreCase));

        var create = await agent.PostAsJsonAsync("api/InternalChat/channels", new CreateImChannelDTO
        {
            Name = "QA Builds",
            ChannelType = "channel",
            Topic = "Quality Assurance & automated test execution"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var channel = (await create.Content.ReadFromJsonAsync<Response<ImChannelDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal("qa-builds", channel.Name);
        Assert.Equal("Quality Assurance & automated test execution", channel.Topic);

        var dup = await agent.PostAsJsonAsync("api/InternalChat/channels", new CreateImChannelDTO
        {
            Name = "qa-builds",
            ChannelType = "channel"
        });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        var parentRes = await agent.PostAsJsonAsync($"api/InternalChat/channels/{channel.Id}/messages",
            new SendImMessageDTO { Body = "Need sign-off on the checkout fix." });
        Assert.Equal(HttpStatusCode.OK, parentRes.StatusCode);
        var parent = (await parentRes.Content.ReadFromJsonAsync<Response<ImMessageDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var replyRes = await agent.PostAsJsonAsync($"api/InternalChat/channels/{channel.Id}/messages",
            new SendImMessageDTO { Body = "I'll re-run the suite after the fix.", ParentMessageId = parent.Id });
        Assert.Equal(HttpStatusCode.OK, replyRes.StatusCode);
        var reply = (await replyRes.Content.ReadFromJsonAsync<Response<ImMessageDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(parent.Id, reply.ParentMessageId);

        var peer = Guid.NewGuid();
        var dmRes = await agent.PostAsJsonAsync("api/InternalChat/dms", new OpenImDmDTO
        {
            UserId = peer,
            DisplayName = "Teammate"
        });
        Assert.Equal(HttpStatusCode.OK, dmRes.StatusCode);
        var dm = (await dmRes.Content.ReadFromJsonAsync<Response<ImChannelDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal("dm", dm.ChannelType);
        Assert.Equal(2, dm.MemberCount);

        var again = await agent.PostAsJsonAsync("api/InternalChat/dms", new OpenImDmDTO
        {
            UserId = peer,
            DisplayName = "Teammate"
        });
        var againBody = (await again.Content.ReadFromJsonAsync<Response<ImChannelDTO>>(SupportApiFactory.JsonOptions))!.Data!;
        Assert.Equal(dm.Id, againBody.Id);
    }

    [Fact]
    public async Task TenantB_cannot_see_TenantA_assets_or_reports()
    {
        var tokenA = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var tokenB = SupportApiFactory.CreateAgentToken(SupportApiFactory.CoreSecret, StaffRoles.Admin);
        var a = _factory.CreateTenantClient(SupportApiFactory.TenantAKey, tokenA);
        var b = _factory.CreateTenantClient(SupportApiFactory.TenantBKey, tokenB);

        var assetRes = await a.PostAsJsonAsync("api/Assets", new SaveAssetDTO { Name = "Secret License", AssetType = "license" });
        var asset = (await assetRes.Content.ReadFromJsonAsync<Response<AssetDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var runRes = await a.PostAsJsonAsync("api/Reports/run", new RunReportDTO { ReportType = "audit" });
        var run = (await runRes.Content.ReadFromJsonAsync<Response<ReportRunDTO>>(SupportApiFactory.JsonOptions))!.Data!;

        var assetsB = await b.GetFromJsonAsync<Response<List<AssetDTO>>>("api/Assets", SupportApiFactory.JsonOptions);
        Assert.DoesNotContain(assetsB!.Data!, x => x.Id == asset.Id);

        var csvB = await b.GetAsync($"api/Reports/{run.Id}/csv");
        Assert.Equal(HttpStatusCode.BadRequest, csvB.StatusCode);
    }
}
