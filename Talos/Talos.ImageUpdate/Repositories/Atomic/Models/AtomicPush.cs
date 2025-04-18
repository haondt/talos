using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public record AtomicPush : IScheduledPush, IPushToFileWriter
    {
        public required List<ISubatomicPushToFileWriter> Writers { get; init; }
        public required BumpSize MaxBumpSize { get; init; }

        [JsonIgnore]
        public string CurrentVersionFriendlyString => string.Join(Environment.NewLine, Writers.Select(q => q.CurrentVersionFriendlyString));
        [JsonIgnore]
        public string NewVersionFriendlyString => string.Join(Environment.NewLine, Writers.Select(q => q.NewVersionFriendlyString));
        [JsonIgnore]
        public BumpSize BumpSize => MaxBumpSize;
        [JsonIgnore]
        public IPushToFileWriter Writer => this;
        [JsonIgnore]
        public string CommitMessage => string.Join(Environment.NewLine, Writers.Select(q => q.CommitMessage));
        [JsonIgnore]
        public IReadOnlyDictionary<string, int> UpdatesPerDomain => Writers.Select(w => w.UpdatesPerDomain)
            .Aggregate(new Dictionary<string, int>(), (d1, d2) =>
            {
                foreach (var (k, v2) in d2)
                {
                    if (!d1.TryGetValue(k, out var v1))
                        v1 = d1[k] = 0;
                    d1[k] = v1 + v2;
                }
                return d1;
            });

        public DetailedResult<IUpdateLocationSnapshot, string> Write(string repositoryDirectory)
        {
            var stagedFileWrites = new Dictionary<string, string>();

            DetailedResult<string, string> stagedFileReader(string relativeFilePath)
            {
                var filePath = Path.Combine(repositoryDirectory, relativeFilePath);
                if (!stagedFileWrites.TryGetValue(filePath, out var fileContent))
                {
                    if (!File.Exists(filePath))
                        return DetailedResult<string, string>.Fail($"Could not find file at {relativeFilePath}");
                    fileContent = stagedFileWrites[filePath] = File.ReadAllText(filePath);
                }
                return DetailedResult<string, string>.Succeed(fileContent);
            }

            void stagedFileWriter(string relativeFilePath, string content)
            {
                stagedFileWrites[Path.Combine(repositoryDirectory, relativeFilePath)] = content;
            }

            var snapshots = new List<ISubatomicUpdateLocationSnapshot>();
            foreach (var writer in Writers)
            {
                var writeResult = writer.StageWrite(stagedFileReader, stagedFileWriter);
                if (!writeResult.IsSuccessful)
                    return new(writeResult.Reason);
                snapshots.Add(writeResult.Value);
            }

            foreach (var (path, data) in stagedFileWrites)
                File.WriteAllText(path, data);

            return new(new AtomicUpdateLocationSnapshot()
            {
                Children = snapshots
            });
        }

        public bool IsNewerThan(IScheduledPush other)
        {
            if (other is not AtomicPush atomicPush)
                return true;
            if (atomicPush.Writers.Count != atomicPush.Writers.Count)
                return true;

            return Writers.Zip(atomicPush.Writers)
                .Any(q => q.First.IsNewerThan(q.Second));
        }

    }
}
