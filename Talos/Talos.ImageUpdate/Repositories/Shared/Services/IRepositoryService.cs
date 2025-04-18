using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Services
{
    public interface IRepositoryService
    {
        Task<bool> CheckIfCommitBelongsToUs(string commit);
        Task<List<(UpdateIdentity Id, IUpdateLocation Location)>> ExtractTargetsAsync(HostConfiguration host, RepositoryConfiguration repositoryConfiguration);
        Task<(List<ScheduledPushWithIdentity> SuccessfulPushes, List<ScheduledPushDeadLetter> FailedPushes)> PushUpdates(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, List<ScheduledPushWithIdentity> scheduledPushes, CancellationToken? cancellationToken = null);
    }
}