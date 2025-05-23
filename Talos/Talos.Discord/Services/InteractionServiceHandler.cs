﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Core.Abstractions;
using Talos.Discord.Abstractions;
using Talos.Discord.Extensions;
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
        private readonly ITracer<InteractionServiceHandler> _tracer;

        public InteractionServiceHandler(
            ITracer<InteractionServiceHandler> tracer,
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
            _tracer = tracer;

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
            using var span = _tracer.StartSpan(nameof(HandleInteractionAsync), SpanKind.Server);
            using (_logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } }))
                try
                {
                    var context = new SocketInteractionContext(_client, interaction);
                    await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
                    span.SetStatusSuccess();
                }
                catch (Exception ex)
                {
                    span.SetStatusFailure(ex.GetType().ToString());
                    throw;
                }
        }

        public Task LogAsync(LogMessage logMessage)
        {
            logMessage.LogTo(_logger);
            return Task.CompletedTask;
        }
    }
}
