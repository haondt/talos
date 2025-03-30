using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
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

    public record AtomicUpdateLocation : IUpdateLocation
    {
        public static AtomicUpdateLocation Create(
            TalosSettings masterConfiguration,


            List<ISubatomicUpdateLocation> children)
        {
            return new()
            {
                Coordinates = new()
                {
                    Children = children.Select(q => q.Coordinates).ToList()
                },
                Locations = children,
                State = new()
                {
                    Configuration = masterConfiguration,
                    Snapshot = new()
                    {
                        Children = children.Select(q => q.SubatomicState.SubatomicSnapshot).ToList()
                    }
                }
            };
        }

        public async Task<Optional<IScheduledPush>> CreateScheduledPushAsync(IImageUpdaterService imageUpdaterService)
        {
            var writers = new List<ISubatomicPushToFileWriter>();
            var maxBumpSize = BumpSize.Digest;
            HashSet<ParsedTag> candidateTagSet = [];
            List<ParsedTag> childCandidateTags = [];
            foreach (var (snapshot, coordinates) in State.Snapshot.Children.Zip(Coordinates.Children))
            {
                childCandidateTags = await imageUpdaterService.GetSortedCandidateTagsAsync(snapshot.CurrentImage, State.Configuration.Bump);
                if (childCandidateTags.Count == 0)
                    return new();

                var childCandidateTagSet = new HashSet<ParsedTag>(childCandidateTags);
                if (candidateTagSet == null)
                    candidateTagSet = childCandidateTagSet;
                else
                    candidateTagSet.IntersectWith(childCandidateTagSet);

                if (candidateTagSet.Count == 0)
                    return new();
            }

            var desiredTag = childCandidateTags.First(t => candidateTagSet.Contains(t));
            var digestSet = new HashSet<string>();
            foreach (var location in Locations)
            {
                var state = location.SubatomicState;
                var snapshot = location.SubatomicState.SubatomicSnapshot;
                var (digest, created) = await imageUpdaterService.GetDigestAsync(snapshot.CurrentImage, desiredTag);
                digestSet.Add(digest);
                if (!imageUpdaterService.IsUpgrade(snapshot.CurrentImage.TagAndDigest, desiredTag, digest).TryGetValue(out var bumpSize))
                    continue;
                maxBumpSize = maxBumpSize > bumpSize ? maxBumpSize : bumpSize;
                writers.Add(location.CreateWriter(new()
                {
                    BumpSize = bumpSize,
                    NewImage = snapshot.CurrentImage with { TagAndDigest = new ParsedTagAndDigest(Tag: desiredTag, Digest: digest) },
                    NewImageCreatedOn = created
                }));
            }

            if (writers.Count == 0)
                return new();

            // skopeo will only return the latest digest so we are going all or nothing
            if (State.Configuration.Sync!.Digest)
                if (digestSet.Count > 0)
                    return new();

            return new AtomicPush()
            {
                MaxBumpSize = maxBumpSize,
                Writers = writers
            };
        }

        public required AtomicUpdateLocationState State { get; init; }
        public required AtomicUpdateLocationCoordinates Coordinates { get; init; }

        [JsonIgnore]
        IUpdateLocationState IUpdateLocation.State => State;

        [JsonIgnore]
        IUpdateLocationCoordinates IUpdateLocation.Coordinates => Coordinates;

        public List<ISubatomicUpdateLocation> Locations { get; init; } = [];
    }

    public record class AtomicUpdateLocationState : IUpdateLocationState
    {
        public required AtomicUpdateLocationSnapshot Snapshot { get; init; }
        public required TalosSettings Configuration { get; init; }

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }


    public record AtomicUpdateLocationSnapshot : IUpdateLocationSnapshot
    {
        public List<ISubatomicUpdateLocationSnapshot> Children { get; init; } = [];

        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not AtomicUpdateLocationSnapshot other)
                return false;
            if (Children.Count != other.Children.Count)
                return false;
            return Children.Zip(other.Children)
                .All(x => x.First.IsEquivalentTo(x.Second));
        }
    }


    public record AtomicUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public List<IUpdateLocationCoordinates> Children { get; set; } = [];

        public UpdateIdentity GetIdentity(string repository)
        {
            return UpdateIdentity.Atomic(repository, Children.Select(q => q.GetIdentity(repository)));
        }

        public override string ToString()
        {
            return $"Atomic:{string.Join(';', Children.Select(q => q.ToString()))}";
        }
    }
}
