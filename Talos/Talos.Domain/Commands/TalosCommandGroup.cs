using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Options;
using Talos.Docker.Abstractions;
using Talos.Domain.Abstractions;
using Talos.Domain.Models;
using Talos.Domain.Models.DiscordEmbedSocket;
using Talos.Domain.Services;
using Talos.Renovate.Abstractions;

namespace Talos.Domain.Commands
{
    [Group("t", "Talos")]
    public partial class TalosCommandGroup(
        IOptions<ApiSettings> apiSettings,
        IWebHookAuthenticationService webhookService,
        IDockerClientFactory dockerClientFactory,
        IPushQueueMutator pushQueueMutator,
        IDiscordCommandProcessRegistry processRegistry)
        : InteractionModuleBase<SocketInteractionContext>, IDiscordEmbedSocketConnector
    {

        [ComponentInteraction("cancel-process-*", ignoreGroupNames: true)]
        public Task CancelProcessAsync(string cancellationId)
        {
            if (Guid.TryParse(cancellationId, out var commandId))
                processRegistry.CancelProcess(commandId);

            return Task.CompletedTask;
        }

        Task IDiscordEmbedSocketConnector.DeferAsync(bool ephemeral, RequestOptions? options)
        {
            return DeferAsync(ephemeral, options);
        }

        Task<IUserMessage> IDiscordEmbedSocketConnector.ModifyOriginalResponseAsync(Action<MessageProperties> func, RequestOptions? options)
        {
            return ModifyOriginalResponseAsync(func, options);
        }

        Task IDiscordEmbedSocketConnector.RespondAsync(string? text, Embed[]? embeds, bool isTTS, bool ephemeral, AllowedMentions? allowedMentions, RequestOptions? options, MessageComponent? components, Embed? embed, PollProperties? poll)
        {
            return RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, components, embed, poll);
        }

        private static Task RenderErrorAsync(DiscordEmbedSocket socket, Exception ex)
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

    }
}
