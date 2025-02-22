// See https://aka.ms/new-console-template for more information
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Discord.Abstractions;
using Talos.Discord.Models;

namespace Talos.Discord.Services;

public class DiscordBot : IDiscordBot
{
    private readonly DiscordSocketClient _client;
    private readonly IInteractionServiceHandler _interactionService;
    private readonly ILogger<DiscordBot> _logger;
    private readonly DiscordSettings _settings;

    public DiscordBot(
        DiscordSocketClient client,
        IOptions<DiscordSettings> discordSettings,
        IInteractionServiceHandler interactionService,
        ILogger<DiscordBot> logger)
    {
        _client = client;
        _interactionService = interactionService;
        _logger = logger;
        _settings = discordSettings.Value;

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
    }

    private async Task ReadyAsync()
    {
        _logger.LogInformation("Ready");
        _logger.LogInformation("Syncing commands...");
        await _interactionService.OnReadyAsync();
        _logger.LogInformation("Command list synced.");
    }

    private async Task AutocompleteHandler(SocketAutocompleteInteraction interaction)
    {
        var option = interaction.Data.Options.First();
        var input = option.Value?.ToString() ?? "";

        List<AutocompleteResult> suggestions;
        if (option.Name == "option1")
        {
            suggestions = new List<AutocompleteResult>
            {
                new("Apple", "Apple"),
                new("Banana", "Banana"),
                new("Cherry", "Cherry")
            };
        }
        else // option2
        {
            suggestions = new List<AutocompleteResult>
            {
                new("Red", "Red"),
                new("Blue", "Blue"),
                new("Green", "Green")
            };
        }

        suggestions = suggestions.Where(s => s.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
        await interaction.RespondAsync(suggestions);
    }



    public async Task StartAsync()
    {
        await _client.LoginAsync(TokenType.Bot, _settings.BotToken);
        await _client.StartAsync();
    }

    public async Task StopAsync()
    {
        await _client.StopAsync();
    }

    private Task LogAsync(LogMessage logMessage)
    {
        _logger.LogInformation(logMessage.ToString());
        return Task.CompletedTask;
    }
}
