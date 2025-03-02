using Discord;

namespace Talos.Discord.Models
{
    public class ExternalDiscordInteractionOperation
    {
    }
    public class ExternalDiscordInteractionCreateOperation : ExternalDiscordInteractionOperation
    {
        public required Embed Embed { get; set; }
        public required MessageComponent Component { get; set; }
    }
    public class ExternalDiscordInteractionDeleteOperation : ExternalDiscordInteractionOperation
    {
        public required string InteractionId { get; set; }
    }
    public class ExternalDiscordInteractionUpdateOperation : ExternalDiscordInteractionOperation
    {
        public required string InteractionId { get; set; }
        public Optional<Embed> Embed { get; set; }
        public Optional<MessageComponent> Component { get; set; }
    }
}
