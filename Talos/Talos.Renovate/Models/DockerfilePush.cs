using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Services;

namespace Talos.Renovate.Models
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

    public record DockerfilePushWriter : ISubatomicPushToFileWriter
    {
        public required ImageUpdateOperation Update { get; init; }
        public required DockerfileUpdateLocationCoordinates Coordinates { get; init; }
        public required DockerfileUpdateLocationSnapshot Snapshot { get; init; }
        [JsonIgnore]
        public string CurrentVersionFriendlyString => Snapshot.CurrentImage.ToShortString();
        [JsonIgnore]
        public string NewVersionFriendlyString => Update.NewImage.ToShortString();
        [JsonIgnore]
        public string CommitMessage => $"{Snapshot.CurrentImage.ToShortString()} -> {Update.NewImage.ToShortString()}";
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

        public DetailedResult<ISubatomicUpdateLocationSnapshot, string> StageWrite(Func<string, DetailedResult<string, string>> fileReader, Action<string, string> fileWriter)
        {
            var fileContentResult = fileReader(Coordinates.RelativeFilePath);
            if (!fileContentResult.IsSuccessful)
                return new(fileContentResult.Reason);
            var fileContent = fileContentResult.Value;
            var fileLines = fileContent.Split(Environment.NewLine);

            if (Coordinates.Line >= fileLines.Length)
                return new($"File is below expected line length {Coordinates.Line + 1}, found {fileLines.Length} lines.");
            var lineHash = HashUtils.ComputeSha256Hash(fileLines[Coordinates.Line]);
            if (!lineHash.SequenceEqual(Snapshot.LineHash))
                return new($"Hash for line {Coordinates.Line} was different than expected.");

            var setResult = DockerfileService.SetFromImage(fileContent, Coordinates.Line, Update.NewImage.ToString());
            if (!setResult.IsSuccessful)
                return new($"Could not update file at {Coordinates.RelativeFilePath}: {setResult.Reason}");

            var (updatedContent, newLine) = setResult.Value;
            fileWriter(Coordinates.RelativeFilePath, updatedContent);
            return new(new DockerfileUpdateLocationSnapshot()
            {
                CurrentImage = Update.NewImage,
                LineHash = HashUtils.ComputeSha256Hash(newLine),
            });
        }

        public bool IsNewerThan(IPushToFileWriter other)
        {
            if (other is not DockerfilePushWriter otherWriter)
                return true;
            if (otherWriter.Update.NewImageCreatedOn >= Update.NewImageCreatedOn)
                return false;
            return true;
        }

        public bool IsNewerThan(ISubatomicPushToFileWriter other) => IsNewerThan((IPushToFileWriter)other);
    }

    public record DockerfileUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public required string RelativeFilePath { get; init; }
        public required int Line { get; init; }

        public UpdateIdentity GetIdentity(string repository)
        {
            return UpdateIdentity.Dockerfile(repository, RelativeFilePath, Line);
        }
        public override string ToString()
        {
            return $"Dockerfile:{RelativeFilePath}:{Line}";
        }

    }

    public record DockerfileUpdateLocation : ISubatomicUpdateLocation
    {

        public required DockerfileUpdateLocationState State { get; init; }
        public required DockerfileUpdateLocationCoordinates Coordinates { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationState SubatomicState => State;

        [JsonIgnore]
        IUpdateLocationState IUpdateLocation.State => State;

        [JsonIgnore]
        IUpdateLocationCoordinates IUpdateLocation.Coordinates => Coordinates;

        public async Task<Optional<IScheduledPush>> CreateScheduledPushAsync(IImageUpdaterService imageUpdaterService)
        {
            var candidateTags = await imageUpdaterService.GetSortedCandidateTagsAsync(State.Snapshot.CurrentImage, State.Configuration.Bump);
            if (candidateTags.Count == 0)
                return new();
            var desiredTag = candidateTags.First();
            var (digest, created) = await imageUpdaterService.GetDigestAsync(State.Snapshot.CurrentImage, desiredTag);
            if (!imageUpdaterService.IsUpgrade(State.Snapshot.CurrentImage.TagAndDigest, desiredTag, digest).TryGetValue(out var bumpSize))
                return new();

            return new(new DockerfilePush()
            {
                Writer = CreateWriter(new()
                {

                    BumpSize = bumpSize,
                    NewImage = State.Snapshot.CurrentImage with { TagAndDigest = new ParsedTagAndDigest(Tag: desiredTag, Digest: digest) },
                    NewImageCreatedOn = created
                })
            });
        }

        public DockerfilePushWriter CreateWriter(ImageUpdateOperation updateOperation)
        {
            return new()
            {
                Coordinates = Coordinates,
                Snapshot = State.Snapshot,
                Update = updateOperation
            };
        }

        ISubatomicPushToFileWriter ISubatomicUpdateLocation.CreateWriter(ImageUpdateOperation updateOperation) => CreateWriter(updateOperation);

    }

    public record DockerfileUpdateLocationState : ISubatomicUpdateLocationState
    {
        public required TalosSettings Configuration { get; init; }
        public required DockerfileUpdateLocationSnapshot Snapshot { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot => Snapshot;

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }
    public record DockerfileUpdateLocationSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ParsedImage CurrentImage { get; init; }
        public required byte[] LineHash { get; init; }

        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not DockerfileUpdateLocationSnapshot other)
                return false;
            return this == other;
        }
    }
}
