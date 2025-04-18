using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
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
            HashSet<ParsedTag>? candidateTagSet = null;
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
            if (candidateTagSet == null)
                return new();

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
}
