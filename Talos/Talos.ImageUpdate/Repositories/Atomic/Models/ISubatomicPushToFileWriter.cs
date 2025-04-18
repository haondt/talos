using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public interface ISubatomicPushToFileWriter : IPushToFileWriter
    {
        string CurrentVersionFriendlyString { get; }
        string NewVersionFriendlyString { get; }
        string CommitMessage { get; }

        bool IsNewerThan(ISubatomicPushToFileWriter other);
        DetailedResult<ISubatomicUpdateLocationSnapshot, string> StageWrite(Func<string, DetailedResult<string, string>> fileReader, Action<string, string> fileWriter);
        IReadOnlyDictionary<string, int> UpdatesPerDomain { get; }
    }



}
