using Haondt.Core.Models;
using Talos.Core.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Dockerfile.Services;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
    public record DockerfilePushWriter : AbstractSubatomicPushWriter<DockerfileUpdateLocationCoordinates, DockerfileUpdateLocationSnapshot>
    {
        protected override DetailedResult<string, string> ReadFileContent(Func<string, DetailedResult<string, string>> fileReader)
        {
            return fileReader(Coordinates.RelativeFilePath);
        }

        protected override DetailedResult<(string NewFileContent, DockerfileUpdateLocationSnapshot Snapshot), string> UpdateFileContent(string fileContent)
        {
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
            return new((updatedContent, new()
            {
                CurrentImage = Update.NewImage,
                LineHash = HashUtils.ComputeSha256Hash(newLine),
            }));
        }

        protected override void WriteFileContent(Action<string, string> fileWriter, string fileContent)
        {
            fileWriter(Coordinates.RelativeFilePath, fileContent);
        }
    }
}
