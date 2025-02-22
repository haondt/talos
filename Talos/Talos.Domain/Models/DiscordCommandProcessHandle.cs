using Talos.Domain.Abstractions;

namespace Talos.Domain.Models
{
    public class DiscordCommandProcessHandle(Action onDisposed) : IDiscordCommandProcessHandle
    {
        public required Guid Id { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public void Dispose()
        {
            onDisposed();
        }
    }
}
