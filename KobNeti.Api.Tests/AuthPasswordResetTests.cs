using System.Net.Http.Json;
using KobNeti.Api.Shared;

namespace KobNeti.Api.Tests;

public class AuthPasswordResetTests : IClassFixture<SupportApiFactory>
{
    private readonly HttpClient _client;

    public AuthPasswordResetTests(SupportApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ForgotPassword_DoesNotRequireTenantHeader()
    {
        var res = await _client.PostAsJsonAsync("api/Auth/forgot-password", new
        {
            email = "nobody@example.com",
            redirectTo = "https://localhost/admin/reset-password"
        });

        Assert.True(res.IsSuccessStatusCode);
        var body = await res.Content.ReadFromJsonAsync<Response<object>>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task ForgotPassword_RejectsEmptyEmail()
    {
        var res = await _client.PostAsJsonAsync("api/Auth/forgot-password", new
        {
            email = "",
            redirectTo = "https://localhost/admin/reset-password"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }
}
