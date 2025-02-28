
namespace Talos.Renovate.Abstractions
{
    public interface IUpdateThrottlingQueueConsumer
    {
        Task RunAsync(CancellationToken? cancellationToken = null);
    }
}