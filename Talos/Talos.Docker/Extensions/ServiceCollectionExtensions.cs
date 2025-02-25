using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talos.Docker.Abstractions;
using Talos.Docker.Models;
using Talos.Docker.Services;

namespace Talos.Docker.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosDockerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
            services.Configure<DockerSettings>(configuration.GetSection(nameof(DockerSettings)));
            return services;
        }
    }
}
