using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Sominnercore.SupportApi.Auth;
using Sominnercore.SupportApi.Data;
using Sominnercore.SupportApi.Services;
using Sominnercore.SupportApi.Tenancy;
using Supabase;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.SectionName));
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddSingleton<ITenantResolver, TenantResolver>();
builder.Services.AddHttpClient();

var supportOptions = builder.Configuration.GetSection(SupportOptions.SectionName).Get<SupportOptions>() ?? new SupportOptions();
var useInMemory = supportOptions.UseInMemoryStore
                  || string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceRoleKey"]);

if (useInMemory)
{
    builder.Services.AddSingleton<ISupportStore, InMemorySupportStore>();
}
else
{
    var url = builder.Configuration["Supabase:Url"] ?? "";
    var key = builder.Configuration["Supabase:ServiceRoleKey"] ?? "";
    var schema = builder.Configuration["Supabase:Schema"] ?? "sominnercore";
    builder.Services.AddSingleton(_ =>
    {
        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            Schema = schema
        };
        var client = new Client(url, key, options);
        client.InitializeAsync().GetAwaiter().GetResult();
        return client;
    });
    builder.Services.AddSingleton<ISupportStore, SupabaseSupportStore>();
}

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IHelpService, HelpService>();
builder.Services.AddScoped<IMacroService, MacroService>();
builder.Services.AddScoped<ISupportCountsService, SupportCountsService>();
builder.Services.AddScoped<IAgentTokenService, AgentTokenService>();

builder.Services
    .AddAuthentication(TenantJwtAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TenantJwtAuthenticationHandler>(
        TenantJwtAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthorizationPolicies.AdminSupport, policy =>
        policy.RequireAssertion(ctx => AdminRoleClaims.CanActAsChatAdmin(ctx.User)));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
