using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talos.Discord.Extensions;
using Talos.Discord.Models;
using Talos.Domain.Abstractions;
using Talos.Domain.Commands;
using Talos.Domain.Models;
using Talos.Domain.Services;
using Talos.Renovate.Abstractions;

namespace Talos.Domain.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DiscordSettings>(configuration.GetSection(nameof(DiscordSettings)));
            services.AddHostedService<TalosService>();

            services.RegisterInteraction<TalosCommandGroup>();
            services.AddSingleton<IDiscordCommandProcessRegistry, DiscordCommandProcessRegistry>();
            services.AddSingleton<INotificationService, DiscordNotificationService>();
            services.AddSingleton<IWebHookAuthenticationService, WebhookAuthenticationService>();
            services.Configure<ApiSettings>(configuration.GetSection(nameof(ApiSettings)));
            return services;
        }
    }
}
