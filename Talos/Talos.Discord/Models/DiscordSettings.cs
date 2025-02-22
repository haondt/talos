namespace Talos.Discord.Models
{
    public class DiscordSettings
    {
        public required string BotToken { get; set; }
        public required ulong GuildId { get; set; }
        public required ulong ChannelId { get; set; }
    }
}
