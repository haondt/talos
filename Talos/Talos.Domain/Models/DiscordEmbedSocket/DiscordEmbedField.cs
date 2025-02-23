using Discord;

namespace Talos.Domain.Models.DiscordEmbedSocket
{
    public class DiscordEmbedField
    {
        public required string Name { get; set; }
        public Haondt.Core.Models.Optional<string> Value { get; set; }
        public bool IsInline = false;

        public DiscordEmbedField Copy()
        {
            return new()
            {
                Name = Name,
                Value = Value,
                IsInline = IsInline
            };
        }

        public EmbedFieldBuilder GetBuilder()
        {
            var builder = new EmbedFieldBuilder();
            builder.Name = Name;
            if (Value.HasValue)
                builder.Value = Value.Value;
            builder.IsInline = IsInline;

            return builder;
        }
    }
}
