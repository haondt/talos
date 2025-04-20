using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.DockerCompose.Services;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
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
        public string CommitMessage => $"{ParsedImage.DiffString(Snapshot.CurrentImage, Update.NewImage.TagAndDigest)}";

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


}
