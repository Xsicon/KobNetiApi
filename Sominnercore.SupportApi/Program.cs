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

builder.Services.AddSingleton<UpstreamApiClient>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IChatService, BridgingChatService>();
builder.Services.AddScoped<HelpService>();
builder.Services.AddScoped<IHelpService, BridgingHelpService>();
builder.Services.AddScoped<IMacroService, MacroService>();
builder.Services.AddScoped<SupportCountsService>();
builder.Services.AddScoped<ISupportCountsService, BridgingSupportCountsService>();
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Som Inner Core Support API",
        Version = "v1",
        Description = "Multi-tenant Chat/Help Support API. Most routes require header X-Tenant-Key."
    });
    options.AddSecurityDefinition("Bearer", new()
    {
        Description = "Agent JWT: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityDefinition("TenantKey", new()
    {
        Description = "Public tenant key (e.g. pk_muuqwear_dev_public)",
        Name = "X-Tenant-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseExceptionHandler(err =>
{
    err.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var message = feature?.Error?.Message ?? "An unexpected error occurred.";
        var logger = context.RequestServices.GetService<ILoggerFactory>()
            ?.CreateLogger("ExceptionHandler");
        logger?.LogError(feature?.Error, "Unhandled exception on {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message
        });
    });
});
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/store", (IConfiguration config, ISupportStore store) =>
{
    var hasServiceRole = !string.IsNullOrWhiteSpace(config["Supabase:ServiceRoleKey"]);
    var useInMemoryFlag = string.Equals(
        config["Support:UseInMemoryStore"], "true", StringComparison.OrdinalIgnoreCase);
    return Results.Ok(new
    {
        store = store.GetType().Name,
        useInMemoryFlag,
        hasServiceRoleKey = hasServiceRole,
        schema = config["Supabase:Schema"] ?? "sominnercore",
        supabaseUrl = config["Supabase:Url"]
    });
});

app.Run();

public partial class Program;
