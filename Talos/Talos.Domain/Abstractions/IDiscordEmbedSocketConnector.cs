using Discord;

namespace Talos.Domain.Abstractions
{
    public interface IDiscordEmbedSocketConnector
    {
        /// <inheritdoc cref="IDiscordInteraction.RespondAsync(string, Embed[], bool, bool, AllowedMentions, MessageComponent, Embed, RequestOptions, PollProperties)"/>
        Task RespondAsync(string? text = null, Embed[]? embeds = null, bool isTTS = false, bool ephemeral = false,
            AllowedMentions? allowedMentions = null, RequestOptions? options = null, MessageComponent? components = null, Embed? embed = null, PollProperties? poll = null);

        /// <inheritdoc cref="IDiscordInteraction.ModifyOriginalResponseAsync(Action{MessageProperties}, RequestOptions)"/>
        Task<IUserMessage> ModifyOriginalResponseAsync(Action<MessageProperties> func, RequestOptions? options = null);

        /// <inheritdoc cref="IDiscordInteraction.DeferAsync(bool, RequestOptions)"/>
        Task DeferAsync(bool ephemeral = false, RequestOptions? options = null);
    }


}
