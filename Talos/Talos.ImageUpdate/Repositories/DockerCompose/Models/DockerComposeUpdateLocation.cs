using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
    public record DockerComposeUpdateLocation : ISubatomicUpdateLocation
    {
        public async Task<Optional<IScheduledPush>> CreateScheduledPushAsync(IImageUpdaterService imageUpdaterService)
        {
            var candidateTags = await imageUpdaterService.GetSortedCandidateTagsAsync(State.Snapshot.CurrentImage, State.Configuration.Bump);
            if (candidateTags.Count == 0)
                return new();
            var desiredTag = candidateTags.First();
            var (digest, created) = await imageUpdaterService.GetDigestAsync(State.Snapshot.CurrentImage, desiredTag);
            if (!imageUpdaterService.IsUpgrade(State.Snapshot.CurrentImage.TagAndDigest, desiredTag, digest).TryGetValue(out var bumpSize))
                return new();

            return new(new DockerComposePush()
            {
                Writer = CreateWriter(new()
                {

                    BumpSize = bumpSize,
                    NewImage = State.Snapshot.CurrentImage with { TagAndDigest = new ParsedTagAndDigest(Tag: desiredTag, Digest: digest) },
                    NewImageCreatedOn = created
                })
            });
        }

        public DockerComposePushWriter CreateWriter(ImageUpdateOperation updateOperation)
        {
            return new()
            {
                Coordinates = Coordinates,
                Snapshot = State.Snapshot,
                Update = updateOperation
            };
        }

        ISubatomicPushToFileWriter ISubatomicUpdateLocation.CreateWriter(ImageUpdateOperation updateOperation) => CreateWriter(updateOperation);

        public required DockerComposeUpdateLocationState State { get; init; }
        public required DockerComposeUpdateLocationCoordinates Coordinates { get; init; }

        [JsonIgnore]
        IUpdateLocationState IUpdateLocation.State => State;

        [JsonIgnore]
        IUpdateLocationCoordinates IUpdateLocation.Coordinates => Coordinates;

        [JsonIgnore]
        public ISubatomicUpdateLocationState SubatomicState => State;
    }


}
