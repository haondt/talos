// See https://aka.ms/new-console-template for more information
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Talos.Discord.Models;

namespace Talos.Domain.Services;

public class TalosService : IHostedService
{
    private readonly IDiscordBot _bot;

    public TalosService(
        IDiscordBot bot,
        IOptions<DiscordSettings> discordSettings)
    {
        _bot = bot;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _bot.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _bot.StopAsync();
    }

    private List<SlashCommandBuilder> BuildSlashCommands()
    {
        var talosCommand = new SlashCommandBuilder()
            .WithName("talos")
            .WithDescription("Talos")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("containers")
                .WithDescription("List containers running on host")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("host")
                    .WithDescription("Host to run command on")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(false)));
        var containersCommand = new SlashCommandBuilder()
            .WithName("containers")
            .WithDescription("List containers running on host")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("list")
                .WithDescription("host to connect to")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("host", ApplicationCommandOptionType.String, "hhost to query", true));
        var command = new SlashCommandBuilder()
            .WithName("example2")
            .WithDescription("An example command with autocomplete.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("optionone")
                .WithDescription("First option")
                .WithType(ApplicationCommandOptionType.String)
                .WithAutocomplete(true)
                .WithRequired(true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("option2")
                .WithDescription("Second option")
                .WithType(ApplicationCommandOptionType.String)
                .WithAutocomplete(true)
                .WithRequired(true));
        return [talosCommand];
    }
}
