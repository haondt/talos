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
        public string SetServiceImage(
            string fileContents, string serviceName, string image)
        {
            var lines = fileContents.Split(Environment.NewLine);
            var outputLines = new List<string>();

            var isInServices = false;
            var isInTargetService = false;
            var success = false;
            var foundImageField = false;
            var foundTargetService = false;
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
                if (!Regex.IsMatch(line, @"^    image:\s*[^&*#\s]\S+\s*$"))
                {
                    outputLines.Add(line);
                    continue;
                }

                outputLines.Add($"    image: {image}");
                success = true;
                foundImageField = true;
            }

            if (success)
                return string.Join(Environment.NewLine, outputLines);

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

            var normalizedUrl = repository.Url.TrimEnd('/');
            foreach (var (absoluteFilePath, relativeFilePath) in GetTargetFiles(clonedRepositoryDirectory, repository))
            {
                var content = File.ReadAllText(absoluteFilePath);
                var yaml = SerializationConstants.DockerComposeDeserializer.Deserialize<DockerComposeFile>(content);
                if (yaml.Services == null || yaml.Services.Count == 0)
                    continue;

                foreach (var service in yaml.Services)
                {
                    var id = new ImageUpdateIdentity(normalizedUrl, relativeFilePath, service.Key);

                    if (string.IsNullOrEmpty(service.Value.Image))
                    {
                        _logger.LogWarning("Skipping image {Image} due to missing image tag...", id);
                        continue;
                    }

                    if (service.Value.XTalos == null)
                    {
                        _logger.LogWarning("Skipping image {Image} due to missing talos extension...", id);
                        continue;
                    }

                    if (service.Value.XTalos.Skip)
                    {
                        _logger.LogInformation("Skipping image {Image} due to configured skip...", id);
                        continue;
                    }

                    images.Add((
                        new(normalizedUrl, relativeFilePath, service.Key),
                        service.Value.XTalos,
                        service.Value.Image));
                }
            }

            return images;
        }
    }
}
