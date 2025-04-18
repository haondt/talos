using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
    public record DockerfilePush : IScheduledPush
    {

        public required DockerfilePushWriter Writer { get; init; }

        [JsonIgnore]
        public BumpSize BumpSize => Writer.Update.BumpSize;

        [JsonIgnore]
        IPushToFileWriter IScheduledPush.Writer => Writer;

        public bool IsNewerThan(IScheduledPush other) => Writer.IsNewerThan(other.Writer);

        [JsonIgnore]
        public string CurrentVersionFriendlyString => Writer.CurrentVersionFriendlyString;
        [JsonIgnore]
        public string NewVersionFriendlyString => Writer.NewVersionFriendlyString;
        [JsonIgnore]
        public string CommitMessage => Writer.CommitMessage;
        [JsonIgnore]
        public IReadOnlyDictionary<string, int> UpdatesPerDomain => Writer.UpdatesPerDomain;

    }
}
