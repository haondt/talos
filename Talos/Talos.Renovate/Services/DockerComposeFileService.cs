using Haondt.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class DockerComposeFileService(ILogger<DockerComposeFileService> _logger) : IDockerComposeFileService
    {
        public (string NewFileContents, Optional<string> PreviousImageString) SetServiceImage(
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
                return (string.Join(Environment.NewLine, outputLines), previousImageString);

            if (!foundTargetService)
                throw new ArgumentException($"Couldn't find service '{serviceName}'");
            if (!foundImageField)
                throw new ArgumentException($"No 'image' field found for service '{serviceName}'.");

            throw new ArgumentException($"Failed to set services.'{serviceName}'.image");
        }


        private static List<(string AbsolutePath, string RelativePath)> GetTargetFiles(string repositoryDirectoryPath, RepositoryConfiguration repository)
        {
            var matcher = new Matcher();
            matcher.AddExclude("**/.git/**");
            if (repository.IncludeGlobs != null)
                matcher.AddIncludePatterns(repository.IncludeGlobs);
            else
                matcher.AddInclude("**/docker-compose.yml");
            if (repository.ExcludeGlobs != null)
                matcher.AddExcludePatterns(repository.ExcludeGlobs);

            var result = matcher.Execute(
                new DirectoryInfoWrapper(
                    new DirectoryInfo(repositoryDirectoryPath)));
            var files = new List<(string, string)>();
            if (result.HasMatches)
                files = result.Files.Select(f => (Path.Join(repositoryDirectoryPath, f.Path), f.Path)).ToList();

            return files;
        }

        public List<(ImageUpdateIdentity Id, TalosSettings Configuration, string Image)> ExtractUpdateTargets(RepositoryConfiguration repository, string clonedRepositoryDirectory)
        {
            var images = new List<(ImageUpdateIdentity, TalosSettings, string)>();

            foreach (var (absoluteFilePath, relativeFilePath) in GetTargetFiles(clonedRepositoryDirectory, repository))
            {
                var content = File.ReadAllText(absoluteFilePath);
                var yaml = SerializationConstants.DockerComposeDeserializer.Deserialize<DockerComposeFile>(content);
                if (yaml.Services == null || yaml.Services.Count == 0)
                    continue;

                foreach (var service in yaml.Services)
                {
                    var id = new ImageUpdateIdentity(repository.NormalizedUrl, relativeFilePath, service.Key);

                    if (string.IsNullOrEmpty(service.Value.Image))
                    {
                        _logger.LogWarning("Skipping image {Image} due to missing image tag...", id);
                        continue;
                    }

                    TalosSettings xTalos;
                    if (service.Value.XTalos != null)
                        xTalos = service.Value.XTalos;
                    else if (!string.IsNullOrEmpty(service.Value.XTalosShort))
                        xTalos = TalosSettings.ParseShortForm(service.Value.XTalosShort);
                    else
                    {
                        _logger.LogWarning("Skipping image {Image} due to missing talos extension...", id);
                        continue;
                    }

                    if (xTalos.Skip)
                    {
                        _logger.LogInformation("Skipping image {Image} due to configured skip...", id);
                        continue;
                    }

                    images.Add((
                        new(repository.NormalizedUrl, relativeFilePath, service.Key),
                        xTalos,
                        service.Value.Image));
                }
            }

            return images;
        }

    }
}
