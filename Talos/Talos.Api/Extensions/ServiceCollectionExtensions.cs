using Haondt.Core.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Talos.Api.Models;
using Talos.Api.Services;
using Talos.Core.Abstractions;

namespace Talos.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHostedService<PipelineListenerService>();

            var tracingSettings = configuration.GetSection<TracingSettings>();

            if (tracingSettings.Enabled)
            {

                services.AddOpenTelemetry()
                    .WithTracing(builder =>
                    {
                        builder.AddSource(
                            "Talos.Api",
                            "Talos.Renovate",
                            "Talos.Discord",
                            "Talos.Domain",
                            "Talos.Core",
                            "Talos.Docker",
                            "Talos.Integration");
                        builder.SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService("Talos"));
                        builder.AddOtlpExporter(o =>
                        {
                            o.Endpoint = new Uri(tracingSettings.Endpoint);
                            o.Protocol = tracingSettings.Protocol;
                        });
                    });
                services.AddSingleton(typeof(ITracer<>), typeof(OpenTelemetryTracer<>));
            }
            else
            {
                services.AddSingleton(typeof(ITracer<>), typeof(EmptyTracer<>));
            }

            return services;
        }
    }
}
