using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Core.Abstractions;
using Talos.Docker.Abstractions;
using Talos.Domain.Abstractions;
using Talos.Domain.Models;
using Talos.Domain.Models.DiscordEmbedSocket;
using Talos.Domain.Services;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.Domain.Commands
{
    [Group("t", "Talos")]
    public partial class TalosCommandGroup(
        ITracer<TalosCommandGroup> tracer,
        ILogger<TalosCommandGroup> logger,
        IOptions<ApiSettings> apiSettings,
        IWebHookAuthenticationService webhookService,
        IDockerClientFactory dockerClientFactory,
        IPushQueueMutator pushQueueMutator,
        IDiscordNotificationService discordNotificationService,
        IDiscordCommandProcessRegistry processRegistry)
        : InteractionModuleBase<SocketInteractionContext>, IDiscordEmbedSocketConnector
    {

        private const string CancelButtonIdPrefix = "cancel-process-";
        private static string CreateCancelButtonId(string processHandleId) => $"{CancelButtonIdPrefix}{processHandleId}";

        [ComponentInteraction($"{CancelButtonIdPrefix}*", ignoreGroupNames: true)]
        public Task CancelProcessAsync(string cancellationId)
        {
            if (Guid.TryParse(cancellationId, out var commandId))
                processRegistry.CancelProcess(commandId);

            return Task.CompletedTask;
        }

        [ComponentInteraction($"{DiscordNotificationService.CompleteInteractionPrefix}-*", ignoreGroupNames: true)]
        public async Task CompleteInteractionAsync(string interactionId)
        {
            using var span = tracer.StartSpan(nameof(CompleteInteractionAsync), SpanKind.Server);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
            await discordNotificationService.CompleteInteractionAsync(interactionId, Context.Interaction);
        }

        async Task IDiscordEmbedSocketConnector.DeferAsync(bool ephemeral, RequestOptions? options)
        {
            using var trace = tracer.StartSpan(nameof(DeferAsync));
            await DeferAsync(ephemeral, options);
        }

        async Task<IUserMessage> IDiscordEmbedSocketConnector.ModifyOriginalResponseAsync(Action<MessageProperties> func, RequestOptions? options)
        {
            using var trace = tracer.StartSpan(nameof(ModifyOriginalResponseAsync));
            trace.SetStatusSuccess();
            try
            {
                return await ModifyOriginalResponseAsync(func, options);
            }
            catch
            {
                trace.SetStatusFailure();
                throw;
            }
        }

        async Task IDiscordEmbedSocketConnector.RespondAsync(string? text, Embed[]? embeds, bool isTTS, bool ephemeral, AllowedMentions? allowedMentions, RequestOptions? options, MessageComponent? components, Embed? embed, PollProperties? poll)
        {
            using var trace = tracer.StartSpan(nameof(RespondAsync));
            await RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, components, embed, poll);
        }

        private Task RenderErrorAsync(DiscordEmbedSocket socket, Exception ex)
        {
            return socket.UpdateAsync(b => b
                .SetColor(Color.Red)
                .AddDescriptionPart("\n**Error**\n")
                .AddDescriptionPart("> " + string.Join("\n> ", ex.Message.Trim().Split('\n'))));
        }

        private async Task RenderErrorAsync(string title, Exception ex)
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this);
            socket.StageUpdate(b => b.AddDescriptionPart($"**{title}**"));
            await RenderErrorAsync(socket, ex);
        }

        private async Task BaseCommand(
            string commandName,
            Action<IDiscordCommandProcessHandle, DiscordEmbedSocketOptions> configureSocketOptions,
            Action<DiscordEmbedSocket> setup,
            Func<IDiscordCommandProcessHandle, DiscordEmbedSocket, Task> execution
            )
        {
            using var span = tracer.StartSpan(commandName, SpanKind.Server);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
            using var processHandle = processRegistry.RegisterProcess();

            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o => configureSocketOptions(processHandle, o));
            setup(socket);
            try
            {
                await execution(processHandle, socket);
                span.SetStatusSuccess();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Command {Command} execution failed", nameof(ContainersCommand));
                span.SetStatusFailure(ex.GetType().ToString());
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
