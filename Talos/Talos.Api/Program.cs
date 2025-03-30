using Haondt.Identity.StorageKey;
using Haondt.Web.Core.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Formatting.Compact;
using Talos.Api.Extensions;
using Talos.Api.Models;
using Talos.Api.Services;
using Talos.Discord.Extensions;
using Talos.Docker.Extensions;
using Talos.Domain.Extensions;
using Talos.Domain.Services;
using Talos.Integration.Command.Extensions;
using Talos.Renovate.Extensions;

StorageKeyConvert.DefaultSerializerSettings.TypeNameStrategy = TypeNameStrategy.SimpleTypeConverter;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.Conditional(
        evt => builder.Environment.IsDevelopment(),
        wt => wt.Seq("http://localhost:5341/"))
    .CreateLogger();

if (builder.Environment.IsDevelopment())
    builder.Configuration.SetBasePath(AppContext.BaseDirectory);

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddTalosDiscordServices(builder.Configuration)
    .AddTalosDockerServices(builder.Configuration)
    .AddTalosRenovateServices(builder.Configuration)
    .AddTalosIntegrationServices(builder.Configuration)
    .AddTalosServices(builder.Configuration)
    .AddTalosApiServices(builder.Configuration)
    .AddSerilog();

builder.Logging.ClearProviders()
    .AddSerilog();

var app = builder.Build();


app.Use(async (context, next) =>
{
    var authService = context.RequestServices.GetRequiredService<IWebHookAuthenticationService>();

    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
        !authHeader.ToString().StartsWith("Bearer "))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Missing or invalid Authorization header.");
        return;
    }

    var token = authHeader.ToString()["Bearer ".Length..];
    var verificationResult = await authService.VerifyApiTokenAsync(token);

    if (!verificationResult.IsSuccessful)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Invalid API token.");
        return;
    }

    await next();
});

var jsonSettings = new JsonSerializerSettings
{
    ContractResolver = new DefaultContractResolver
    {
        NamingStrategy = new SnakeCaseNamingStrategy() // Enables snake_case conversion
    },
    NullValueHandling = NullValueHandling.Ignore,
    MissingMemberHandling = MissingMemberHandling.Ignore
};

app.MapPost("/webhooks/{integration}", async (HttpContext context, string integration) =>
{
    try
    {
        switch (integration)
        {
            case "gitlab":
                if (context.Request.Headers.TryGetValue<string>(GitLabConstants.GITLAB_EVENT_HEADER, out var eventHeader))
                    if (!GitLabConstants.GITLAB_EVENT_PIPELINE.Equals(eventHeader, StringComparison.OrdinalIgnoreCase))
                        return Results.Ok();
                break;
        }

        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        PipelineEventDto? payload = integration switch
        {
            "gitlab" => JsonConvert.DeserializeObject<GitLabPipelineEventDto>(body, jsonSettings),
            _ => null
        };

        if (payload == null)
            return Results.BadRequest("Invalid request payload.");

        await PipelineListenerChannelProvider.Channel.Writer.WriteAsync(payload);

        return Results.Ok();
    }
    catch (JsonException ex)
    {
        app.Logger.LogError(ex, "Ran into serialization error during gitlab webhook: {Error}", ex.Message);
        return Results.BadRequest("Malformed JSON.");
    }
});

app.Run();
