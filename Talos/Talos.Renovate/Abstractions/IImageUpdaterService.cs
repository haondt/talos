
using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdaterService
    {
        Task RunUpdateAsync(CancellationToken? cancellationToken = null);
        Task<Optional<ImageUpdate>> SelectUpdateTarget(string image, BumpSize maxBumpSize, bool insertDefaultDomain = true);
        Task RunAsync(CancellationToken? cancellationToken = null);
        (HostConfiguration Host, RepositoryConfiguration Repository) GetRepositoryConfiguration(string remoteUrl);
        Task PushUpdates(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, List<ScheduledPush> scheduledPushes, CancellationToken? cancellationToken = null);
    }
}