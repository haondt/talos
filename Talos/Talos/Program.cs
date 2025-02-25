using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Talos.Discord.Extensions;
using Talos.Docker.Extensions;
using Talos.Domain.Extensions;
using Talos.Integration.Command.Extensions;
using Talos.Renovate.Extensions;

using var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json");
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json");
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddTalosDiscordServices(context.Configuration);
        services.AddTalosDockerServices(context.Configuration);
        services.AddTalosRenovateServices(context.Configuration);
        services.AddTalosCommandServices(context.Configuration);
        services.AddTalosServices(context.Configuration);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

await host.RunAsync();
