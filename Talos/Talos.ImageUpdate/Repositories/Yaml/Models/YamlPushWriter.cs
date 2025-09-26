using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;

namespace Talos.ImageUpdate.Repositories.Yaml.Models
{
    public record YamlPushWriter : AbstractSubatomicPushWriter<YamlUpdateLocationCoordinates, YamlUpdateLocationSnapshot>
    {
        protected override DetailedResult<string, string> ReadFileContent(Func<string, DetailedResult<string, string>> fileReader)
        {
            return fileReader(Coordinates.RelativeFilePath);
        }

        protected override DetailedResult<(string NewFileContent, YamlUpdateLocationSnapshot Snapshot), string> UpdateFileContent(string fileContent)
        {
            var previousImageString = fileContent[Coordinates.Start..Coordinates.End];
            var updatedContent = fileContent[..Coordinates.Start] + Update.NewImage.ToString() + fileContent[Coordinates.End..];

            if (!previousImageString.Trim().Equals(Snapshot.RawCurrentImageString.Trim()))
                return new($"Expected previous image '{Snapshot.RawCurrentImageString.Trim()}' does not match the actual previous image '{previousImageString.Trim()}'");

            return new((updatedContent, new()
            {
                CurrentImage = Update.NewImage,
                RawCurrentImageString = Update.NewImage.ToString(),
            }));
        }

        protected override void WriteFileContent(Action<string, string> fileWriter, string fileContent)
        {
            fileWriter(Coordinates.RelativeFilePath, fileContent);
        }
    }
}

