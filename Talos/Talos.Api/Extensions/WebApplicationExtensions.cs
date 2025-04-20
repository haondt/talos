using Haondt.Web.Core.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Talos.Api.Models;
using Talos.Api.Services;
using Talos.Domain.Services;

namespace Talos.Api.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication AddTalosApiEndpoints(this WebApplication app)
        {
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
            return app;
        }
    }
}
