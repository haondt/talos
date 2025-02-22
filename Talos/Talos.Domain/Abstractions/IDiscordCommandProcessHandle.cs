namespace Talos.Domain.Abstractions
{
    public interface IDiscordCommandProcessHandle : IDisposable
    {
        Guid Id { get; }
        CancellationToken CancellationToken { get; }
    }
}
