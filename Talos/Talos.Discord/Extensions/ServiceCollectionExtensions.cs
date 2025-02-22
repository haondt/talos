using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talos.Discord.Abstractions;
using Talos.Discord.Models;
using Talos.Discord.Services;

namespace Talos.Discord.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosDiscordServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IDiscordBot, DiscordBot>();
            services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds,
            }));
            services.AddSingleton(provider =>
            {
                var client = provider.GetRequiredService<DiscordSocketClient>();
                return new InteractionService(client, new()
                {
                    DefaultRunMode = RunMode.Async
                });
            });
            services.AddSingleton<IInteractionServiceHandler, InteractionServiceHandler>();
            return services;
        }

        public static IServiceCollection RegisterInteraction<T>(this IServiceCollection services) where T : IInteractionModuleBase
        {
            services.AddSingleton<IRegisteredInteractionModule, RegisteredInteractionModule>(_ => new RegisteredInteractionModule
            {
                Type = typeof(T)
            });
            return services;
        }

    }
}
