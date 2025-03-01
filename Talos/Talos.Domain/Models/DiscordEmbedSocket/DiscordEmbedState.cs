using Discord;
using System.Text;
using Talos.Domain.Abstractions;

namespace Talos.Domain.Models.DiscordEmbedSocket
{
    public class DiscordEmbedDescriptionPart
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Value { get; set; }
    }


    public class DiscordEmbedState
    {
        private static readonly Color INITIAL_EMBED_COLOR = Color.Parse("#e8ce25");
        private static readonly int MAX_DESCRIPTION_LENGTH = 1024;
        private static readonly string TRUNCATED_TEXT_MARKER = "\n-# (truncated)";

        public Haondt.Core.Models.Optional<string> Title { get; set; }
        public Haondt.Core.Models.Optional<string> CancelButtonId { get; set; }
        public Haondt.Core.Models.Optional<Color> DefaultColor { get; set; }
        public List<DiscordEmbedField> Fields = [];
        public List<DiscordEmbedDescriptionPart> DescriptionParts = [];

        public bool Emphemeral { get; set; } = false;
        public bool Dirty { get; set; } = true;
        public bool HasSentInitialResponse { get; set; } = false;

        private ButtonBuilder BuildCancelButton()
        {
            return new ButtonBuilder()
                .WithCustomId(CancelButtonId.Value)
                .WithLabel("Cancel")
                .WithStyle(ButtonStyle.Danger);
        }

        private async Task CreateOrUpdateEmbedAsync(IDiscordEmbedSocketConnector connector,
            Embed embed, MessageComponent? components = null)
        {
            components ??= new ComponentBuilder().Build();

            if (HasSentInitialResponse)
            {
                await connector.ModifyOriginalResponseAsync(m => { m.Embed = embed; m.Components = new ComponentBuilder().Build(); });
            }
            else
            {
                await connector.RespondAsync(embed: embed, components: components, ephemeral: Emphemeral);
                HasSentInitialResponse = true;
            }
        }

        public Task SendEmbedAsync(IDiscordEmbedSocketConnector connector)
        {
            var builder = new EmbedBuilder();
            if (Title.HasValue)
                builder = builder.WithTitle(Title.Value);

            if (DescriptionParts.Count > 0)
            {
                var descriptionSb = new StringBuilder();
                foreach (var part in DescriptionParts)
                    descriptionSb.AppendLine(part.Value);
                var description = descriptionSb.ToString();
                if (description.Length > MAX_DESCRIPTION_LENGTH)
                    description = description.Substring(0, MAX_DESCRIPTION_LENGTH - TRUNCATED_TEXT_MARKER.Length) + TRUNCATED_TEXT_MARKER;
                builder = builder.WithDescription(description);
            }
            if (DefaultColor.HasValue)
                builder = builder.WithColor(DefaultColor.Value);
            if (Fields.Count > 0)
                builder = builder.WithFields(Fields.Select(f => f.GetBuilder()).ToArray());

            builder.Timestamp = DateTimeOffset.UtcNow;

            if (!CancelButtonId.HasValue)
                return CreateOrUpdateEmbedAsync(connector, embed: builder.Build());

            var button = BuildCancelButton();
            var component = new ComponentBuilder()
                .WithButton(button)
                .Build();

            return CreateOrUpdateEmbedAsync(connector, embed: builder.Build(), components: component);
        }
    }
}
