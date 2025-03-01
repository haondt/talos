using Talos.Api.Services;

namespace Talos.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHostedService<PipelineListenerService>();
            return services;
        }
    }
}
