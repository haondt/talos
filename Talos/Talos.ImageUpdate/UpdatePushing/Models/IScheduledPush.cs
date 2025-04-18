using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.UpdatePushing.Models
{

    public interface IScheduledPush
    {
        BumpSize BumpSize { get; }

        IPushToFileWriter Writer { get; }
        string CurrentVersionFriendlyString { get; }
        string NewVersionFriendlyString { get; }
        string CommitMessage { get; }

        bool IsNewerThan(IScheduledPush other);
        IReadOnlyDictionary<string, int> UpdatesPerDomain { get; }
    }



}
