using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public abstract record AbstractSubatomicPushWriter<TCoordinates, TSnapshot> : ISubatomicPushToFileWriter
        where TCoordinates : IUpdateLocationCoordinates
        where TSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ImageUpdateOperation Update { get; init; }
        public required TCoordinates Coordinates { get; init; }
        public required TSnapshot Snapshot { get; init; }
        [JsonIgnore]
        public string CurrentVersionFriendlyString => Snapshot.CurrentImage.ToShortString();
        [JsonIgnore]
        public string NewVersionFriendlyString => Update.NewImage.ToShortString();
        [JsonIgnore]
        public string CommitMessage => $"{ParsedImage.DiffString(Snapshot.CurrentImage, Update.NewImage.TagAndDigest)}";
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

        protected abstract DetailedResult<string, string> ReadFileContent(Func<string, DetailedResult<string, string>> fileReader);
        protected abstract DetailedResult<(string NewFileContent, TSnapshot Snapshot), string> UpdateFileContent(string fileContent);
        protected abstract void WriteFileContent(Action<string, string> fileWriter, string fileContent);

        public DetailedResult<ISubatomicUpdateLocationSnapshot, string> StageWrite(Func<string, DetailedResult<string, string>> fileReader, Action<string, string> fileWriter)
        {
            var fileContentResult = ReadFileContent(fileReader);
            if (!fileContentResult.IsSuccessful)
                return new(fileContentResult.Reason);
            var fileContent = fileContentResult.Value;
            var setResult = UpdateFileContent(fileContent);
            if (!setResult.IsSuccessful)
                return new($"Could not update file at {Coordinates}: {setResult.Reason}");

            var (updatedContent, newSnapshot) = setResult.Value;
            WriteFileContent(fileWriter, updatedContent);
            return new(newSnapshot);
        }

        public bool IsNewerThan(IPushToFileWriter other)
        {
            if (other is not AbstractSubatomicPushWriter<TCoordinates, TSnapshot> otherWriter)
                return true;
            if (otherWriter.Update.NewImageCreatedOn >= Update.NewImageCreatedOn)
                return false;
            return true;
        }

        public bool IsNewerThan(ISubatomicPushToFileWriter other) => IsNewerThan((IPushToFileWriter)other);
    }
}
