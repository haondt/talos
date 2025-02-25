using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talos.Integration.Command.Abstractions;
using Talos.Integration.Command.Models;
using Talos.Integration.Command.Services;

namespace Talos.Integration.Command.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosCommandServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICommandFactory, CommandFactory>();
            services.Configure<CommandSettings>(configuration.GetSection(nameof(CommandSettings)));
            return services;
        }
    }
}
