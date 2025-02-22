using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talos.Discord.Extensions;
using Talos.Discord.Models;
using Talos.Domain.Commands;
using Talos.Domain.Services;

namespace Talos.Domain.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DiscordSettings>(configuration.GetSection(nameof(DiscordSettings)));
            services.AddHostedService<TalosService>();

            services.RegisterInteraction<TalosCommandGroup>();
            return services;
        }
    }
}
