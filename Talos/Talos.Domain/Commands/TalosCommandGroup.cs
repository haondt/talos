using Discord;
using Discord.Interactions;
using Talos.Docker.Abstractions;
using Talos.Domain.Abstractions;

namespace Talos.Domain.Commands
{
    [Group("t", "Talos")]
    public partial class TalosCommandGroup(
        IDockerClientFactory dockerClientFactory,
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

    }
}
