using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Services;

namespace Talos.Renovate.Models
{
    public record DockerComposePush : IScheduledPush
    {
        public required DockerComposePushWriter Writer { get; init; }

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
    public record DockerComposePushWriter : ISubatomicPushToFileWriter
    {
        public required ImageUpdateOperation Update { get; init; }
        public required DockerComposeUpdateLocationCoordinates Coordinates { get; init; }
        public required DockerComposeUpdateLocationSnapshot Snapshot { get; init; }
        [JsonIgnore]
        public string CurrentVersionFriendlyString => Snapshot.CurrentImage.ToShortString();
        [JsonIgnore]
        public string NewVersionFriendlyString => Update.NewImage.ToShortString();
        [JsonIgnore]
        public IReadOnlyDictionary<string, int> UpdatesPerDomain => new Dictionary<string, int> { [Update.NewImage.Domain.Or("")] = 1 };
        public DetailedResult<IUpdateLocationSnapshot, string> Write(string repositoryDirectory)
        {
            var stagedFileWrites = new Dictionary<string, string>();

            DetailedResult<string, string> stagedFileReader(string relativeFilePath)
            {
                var filePath = Path.Combine(repositoryDirectory, relativeFilePath);
                if (!File.Exists(filePath))
                    return DetailedResult<string, string>.Fail($"Could not find file at {relativeFilePath}");
                var fileContent = File.ReadAllText(filePath);
                return DetailedResult<string, string>.Succeed(fileContent);
            }
            void stagedFileWriter(string relativeFilePath, string content)
            {
                File.WriteAllText(Path.Combine(repositoryDirectory, relativeFilePath), content);
            }

            var result = StageWrite(stagedFileReader, stagedFileWriter);
            if (result.IsSuccessful)
                return new(result.Value);
            return new(result.Reason);
        }

        [JsonIgnore]
        public string CommitMessage => $"{Snapshot.CurrentImage.ToShortString()} -> {Update.NewImage.ToShortString()}";

        public DetailedResult<ISubatomicUpdateLocationSnapshot, string> StageWrite(Func<string, DetailedResult<string, string>> fileReader, Action<string, string> fileWriter)
        {
            var fileContentResult = fileReader(Coordinates.RelativeFilePath);
            if (!fileContentResult.IsSuccessful)
                return new(fileContentResult.Reason);
            var fileContent = fileContentResult.Value;

            var setResult = DockerComposeFileService.SetServiceImage(fileContent, Coordinates.ServiceKey, Update.NewImage.ToString());
            if (!setResult.IsSuccessful)
                return new($"Could not find target at {Coordinates.RelativeFilePath}:{Coordinates.ServiceKey}: {setResult.Reason}");

            var (updatedContent, previousImageString) = setResult.Value;
            if (!previousImageString.Trim().Equals(Snapshot.RawCurrentImageString.Trim()))
                return new($"Expected previous image '{Snapshot.RawCurrentImageString.Trim()}' does not match the actual previous image '{previousImageString.Trim()}'");

            fileWriter(Coordinates.RelativeFilePath, updatedContent);
            return new(new DockerComposeUpdateLocationSnapshot()
            {
                CurrentImage = Update.NewImage,
                RawCurrentImageString = Update.NewImage.ToString(),
            });
        }

        public bool IsNewerThan(IPushToFileWriter other)
        {
            if (other is not DockerComposePushWriter otherWriter)
                return true;
            if (otherWriter.Update.NewImageCreatedOn >= Update.NewImageCreatedOn)
                return false;
            return true;
        }
        public bool IsNewerThan(ISubatomicPushToFileWriter other) => IsNewerThan((IPushToFileWriter)other);

    }
    public record DockerComposeUpdateLocationState : ISubatomicUpdateLocationState
    {
        public required TalosSettings Configuration { get; init; }
        public required DockerComposeUpdateLocationSnapshot Snapshot { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot => Snapshot;

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }
    public record DockerComposeUpdateLocationSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ParsedImage CurrentImage { get; init; }
        public required string RawCurrentImageString { get; init; }


        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not DockerComposeUpdateLocationSnapshot other)
                return false;

            return this == other;
        }
    }



    public record DockerComposeUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public required string RelativeFilePath { get; init; }
        public required string ServiceKey { get; init; }

        public UpdateIdentity GetIdentity(string repository)
        {
            return UpdateIdentity.DockerCompose(repository, RelativeFilePath, ServiceKey);
        }

        public override string ToString()
        {
            return $"DockerCompose:{RelativeFilePath}:{ServiceKey}";
        }
    }
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
