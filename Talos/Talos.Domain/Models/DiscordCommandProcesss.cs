namespace Talos.Domain.Models
{
    public class DiscordCommandProcesss
    {
        public required Guid Id { get; set; }
        public required CancellationTokenSource CancellationTokenSource { get; set; }
    }
}