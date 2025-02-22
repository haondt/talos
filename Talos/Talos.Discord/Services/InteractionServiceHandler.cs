using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Discord.Abstractions;
using Talos.Discord.Models;

namespace Talos.Discord.Services
{
    public class InteractionServiceHandler : IInteractionServiceHandler
    {
        private readonly IOptions<DiscordSettings> _discordOptions;
        private readonly ILogger<InteractionServiceHandler> _logger;
        private readonly InteractionService _interactionService;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IRegisteredInteractionModule> _registrations;

        public InteractionServiceHandler(
            IOptions<DiscordSettings> discordOptions,
            InteractionService interactionService,
            DiscordSocketClient client,
            IServiceProvider serviceProvider,
            ILogger<InteractionServiceHandler> logger,
            IEnumerable<IRegisteredInteractionModule> registrations)
        {
            _discordOptions = discordOptions;
            _logger = logger;
            _interactionService = interactionService;
            _client = client;
            _serviceProvider = serviceProvider;
            _registrations = registrations;

            _client.InteractionCreated += HandleInteractionAsync;

            _interactionService.Log += LogAsync;

        }

        public async Task OnReadyAsync()
        {
            foreach (var registration in _registrations)
                await _interactionService.AddModuleAsync(registration.Type, _serviceProvider);
            await _interactionService.RegisterCommandsToGuildAsync(_discordOptions.Value.GuildId);
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }

        public Task LogAsync(LogMessage logMessage)
        {
            var logLevel = logMessage.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Trace,
                _ => LogLevel.Information
            };

            if (logMessage.Exception != null)
            {
                if (!string.IsNullOrEmpty(logMessage.Message))
                    _logger.Log(
                        logLevel,
                        logMessage.Exception,
                        "[InteractionService] [{Source}] {Message}",
                        logMessage.Source,
                        logMessage.Message);
                else
                    _logger.Log(
                        logLevel,
                        logMessage.Exception,
                        "[InteractionService] [{Source}]",
                        logMessage.Source);
            }
            else
            {

                if (!string.IsNullOrEmpty(logMessage.Message))
                    _logger.Log(
                        logLevel,
                        "[InteractionService] [{Source}] {Message}",
                        logMessage.Source,
                        logMessage.Message);
                else
                    _logger.Log(
                        logLevel,
                        "[InteractionService] [{Source}]",
                        logMessage.Source);

            }

            return Task.CompletedTask;
        }
    }
}
