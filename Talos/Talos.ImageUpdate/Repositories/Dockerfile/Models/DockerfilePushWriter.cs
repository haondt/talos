using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.Core.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Dockerfile.Services;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
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

            var setResult = DockerfileFileService.SetFromImage(fileContent, Coordinates.Line, Update.NewImage.ToString());
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
}
