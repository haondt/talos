using Haondt.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.RegularExpressions;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.ImageParsing;
using Talos.ImageUpdate.Repositories.DockerCompose.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Repositories.Shared.Services;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Services
{
    public class DockerComposeFileService(IImageParser imageParser) : IRepositoryFileService
    {
        public static DetailedResult<(string NewFileContents, string PreviousImageString), string> SetServiceImage(
            string fileContents, string serviceName, string image)
        {
            var lines = fileContents.Split(Environment.NewLine);
            var outputLines = new List<string>();

            var isInServices = false;
            var isInTargetService = false;
            var success = false;
            var foundImageField = false;
            var foundTargetService = false;
            var previousImageString = new Optional<string>();
            foreach (var line in lines)
            {
                if (success)
                {
                    outputLines.Add(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || Regex.IsMatch(line, @"\s*#.*$"))
                {
                    outputLines.Add(line);
                    continue;
                }

                if (Regex.IsMatch(line, @"^services:\s*$"))
                {
                    isInServices = true;
                    isInTargetService = false;
                    outputLines.Add(line);
                    continue;
                }

                if (Regex.IsMatch(line, @"^\S+:\s*$"))
                {
                    isInServices = false;
                    isInTargetService = false;
                    outputLines.Add(line);
                    continue;
                }

                if (!isInServices)
                {
                    outputLines.Add(line);
                    continue;
                }

                var serviceMatch = Regex.Match(line, @"^  (?<service>\S+):\s*$");
                if (serviceMatch.Success)
                {
                    isInTargetService = serviceMatch.Groups["service"].Value == serviceName;
                    outputLines.Add(line);
                    continue;
                }

                if (!isInTargetService)
                {
                    outputLines.Add(line);
                    continue;
                }

                foundTargetService = true;
                var previousImageMatch = Regex.Match(line, @"^    image:\s*(?<image>[^&*#\s]\S+)\s*$");
                if (!previousImageMatch.Success)
                {
                    outputLines.Add(line);
                    continue;
                }

                previousImageString = previousImageMatch.Groups["image"].Value;
                outputLines.Add($"    image: {image}");
                success = true;
                foundImageField = true;
            }

            if (success)
                return new((string.Join(Environment.NewLine, outputLines), previousImageString.Value!));

            if (!foundTargetService)
                return new($"Couldn't find service '{serviceName}'");
            if (!foundImageField)
                return new($"No 'image' field found for service '{serviceName}'.");

            return new($"Failed to set services.'{serviceName}'.image");
        }


        private static List<(string AbsolutePath, string RelativePath)> GetTargetFiles(string repositoryDirectoryPath, RepositoryConfiguration repository)
        {
            var matcher = new Matcher();
            matcher.AddExclude("**/.git/**");
            if (repository.Glob.DockerCompose?.IncludeGlobs != null)
                matcher.AddIncludePatterns(repository.Glob.DockerCompose.IncludeGlobs);
            else
                matcher.AddInclude("**/docker-compose.yml");
            if (repository.Glob.DockerCompose?.ExcludeGlobs != null)
                matcher.AddExcludePatterns(repository.Glob.DockerCompose.ExcludeGlobs);

            var result = matcher.Execute(
                new DirectoryInfoWrapper(
                    new DirectoryInfo(repositoryDirectoryPath)));
            var files = new List<(string, string)>();
            if (result.HasMatches)
                files = result.Files.Select(f => (Path.Join(repositoryDirectoryPath, f.Path), f.Path)).ToList();

            return files;
        }

        public List<DetailedResult<IUpdateLocation, string>> ExtractLocations(RepositoryConfiguration repository, string clonedRepositoryDirectory)
        {
            var images = new List<DetailedResult<IUpdateLocation, string>>();
            if (repository.Glob.DockerCompose == null)
                return images;

            foreach (var (absoluteFilePath, relativeFilePath) in GetTargetFiles(clonedRepositoryDirectory, repository))
            {
                var content = File.ReadAllText(absoluteFilePath);
                var yaml = SerializationConstants.DockerComposeDeserializer.Deserialize<DockerComposeFile>(content);
                if (yaml.Services == null || yaml.Services.Count == 0)
                    continue;

                foreach (var service in yaml.Services)
                {
                    var coordinates = new DockerComposeUpdateLocationCoordinates { RelativeFilePath = relativeFilePath, ServiceKey = service.Key };


                    TalosSettings xTalos;
                    if (service.Value.XTalos != null)
                        xTalos = service.Value.XTalos;
                    else if (!string.IsNullOrEmpty(service.Value.XTalosShort))
                    {
                        var shortFormParsedResult = TalosSettings.ParseShortForm(service.Value.XTalosShort);
                        if (!shortFormParsedResult.IsSuccessful)
                        {
                            images.Add(new(shortFormParsedResult.Reason));
                            continue;
                        }
                        xTalos = shortFormParsedResult.Value;
                    }
                    else
                    {
                        images.Add(new($"{coordinates}: missing talos extension"));
                        continue;
                    }

                    if (xTalos.Skip)
                        continue;

                    if (string.IsNullOrEmpty(service.Value.Image))
                    {
                        images.Add(new($"{coordinates}: missing image tag"));
                        continue;
                    }


                    var parsedImage = imageParser.TryParse(service.Value.Image, true);
                    if (!parsedImage.HasValue)
                    {
                        images.Add(new($"{coordinates}: couldn't parse image {service.Value.Image}"));
                        continue;
                    }

                    var state = new DockerComposeUpdateLocationState
                    {
                        Configuration = xTalos,
                        Snapshot = new()
                        {
                            RawCurrentImageString = service.Value.Image,
                            CurrentImage = parsedImage.Value
                        }
                    };

                    images.Add(new(new DockerComposeUpdateLocation
                    {
                        Coordinates = coordinates,
                        State = state
                    }));
                }
            }

            return images;
        }

    }
}
