using gfs.YamlDotNet.YamlPath;
using Haondt.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.ImageParsing;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Repositories.Shared.Services;
using Talos.ImageUpdate.Repositories.Yaml.Models;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.Shared.Models;
using YamlDotNet.RepresentationModel;

namespace Talos.ImageUpdate.Repositories.Yaml.Services
{
    public class YamlFileService(IImageParser imageParser) : IRepositoryFileService
    {
        private static List<(string AbsolutePath, string RelativePath)> GetTargetFiles(string repositoryDirectoryPath, RepositoryConfiguration repository)
        {
            var matcher = new Matcher();
            matcher.AddExclude("**/.git/**");
            if (repository.Glob.Yaml?.IncludeGlobs != null)
                matcher.AddIncludePatterns(repository.Glob.Yaml.IncludeGlobs);
            else
            {
                matcher.AddInclude("**/*.yml");
                matcher.AddInclude("**/*.yaml");
            }
            if (repository.Glob.Yaml?.ExcludeGlobs != null)
                matcher.AddExcludePatterns(repository.Glob.Yaml.ExcludeGlobs);

            var result = matcher.Execute(
                new DirectoryInfoWrapper(
                    new DirectoryInfo(repositoryDirectoryPath)));
            var files = new List<(string, string)>();
            if (result.HasMatches)
                files = result.Files.Select(f => (Path.Join(repositoryDirectoryPath, f.Path), f.Path)).ToList();
            return files;
        }
        private static DetailedResult<YamlNode, string> ExtractNodeFromPath(YamlNode mapping, string path)
        {
            static DetailedResult<YamlNode, string> Fail(string s) => DetailedResult<YamlNode, string>.Fail(s);

            var pathMatches = ExtractNodesFromPath(mapping, path);
            if (pathMatches.Count != 1)
                return Fail($"Found {pathMatches.Count} matches, but was expecting 1");
            return new(pathMatches[0]);
        }

        private static List<YamlNode> ExtractNodesFromPath(YamlNode mapping, string path)
        {
            return mapping.Query(path).ToList();
        }

        public List<DetailedResult<IUpdateLocation, string>> ExtractLocations(RepositoryConfiguration repositoryConfiguration, string repositoryDirectory)
        {
            var images = new List<DetailedResult<IUpdateLocation, string>>();
            if (repositoryConfiguration.Glob.Yaml == null)
                return images;

            foreach (var (absoluteFilePath, relativeFilePath) in GetTargetFiles(repositoryDirectory, repositoryConfiguration))
            {

                var content = File.ReadAllText(absoluteFilePath);
                var yaml = new YamlStream();
                yaml.Load(new StringReader(content));
                var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

                foreach (var node in ExtractNodesFromPath(mapping, repositoryConfiguration.Glob.Yaml.AncestorPath))
                {
                    var coordinates = new YamlUpdateLocationCoordinates { RelativeFilePath = relativeFilePath, Start = (int)node.Start.Index, End = (int)node.End.Index };

                    TalosSettings talosSettings;
                    if (ExtractNodeFromPath(node, repositoryConfiguration.Glob.Yaml.RelativeTalosPath) is not { IsSuccessful: true, Value: var talosNode })
                    {
                        images.Add(new($"{coordinates}: missing talos extension"));
                        continue;
                    }

                    if (talosNode is YamlScalarNode scalarTalosNode)
                    {
                        if (scalarTalosNode.Value is not { } scalarTalosNodeValue)
                        {
                            images.Add(new($"{coordinates}: failed to parse short talos form"));
                            continue;
                        }

                        var shortTalosParseResult = TalosSettings.ParseShortForm(scalarTalosNodeValue);
                        if (!shortTalosParseResult.IsSuccessful)
                        {
                            images.Add(new($"{coordinates}: {shortTalosParseResult.Reason}"));
                            continue;
                        }

                        talosSettings = shortTalosParseResult.Value;
                    }
                    else if (talosNode is YamlMappingNode mappingTalosNode)
                    {
                        var nodeText = SerializationConstants.YamlSerializer.Serialize(mappingTalosNode);
                        var nodeValue = SerializationConstants.YamlDeserializer.Deserialize<TalosSettings>(nodeText);
                        if (nodeValue is not { } value)
                        {
                            images.Add(new($"{coordinates}: failed to reserialize value"));
                            continue;
                        }

                        talosSettings = value;
                    }
                    else
                    {
                        images.Add(new($"{coordinates}: talos node was not a scalar or a map"));
                        continue;
                    }

                    if (talosSettings.Skip)
                        continue;

                    var imageResult = ExtractNodeFromPath(node, repositoryConfiguration.Glob.Yaml.RelativeImagePath);
                    if (imageResult is not { IsSuccessful: true, Value: var imageNode }
                        || imageNode is not YamlScalarNode scalarImageNode
                        || scalarImageNode.Value is not { } image)
                    {
                        images.Add(new($"{coordinates}: failed to retrieve image"));
                        continue;
                    }
                    coordinates = new YamlUpdateLocationCoordinates { RelativeFilePath = relativeFilePath, Start = (int)imageNode.Start.Index, End = (int)imageNode.End.Index };

                    var parsedImage = imageParser.TryParse(image, true);
                    if (!parsedImage.HasValue)
                    {
                        images.Add(new($"{coordinates}: couldn't parse image {image}"));
                        continue;
                    }

                    var state = new YamlUpdateLocationState
                    {
                        Configuration = talosSettings,
                        Snapshot = new()
                        {
                            RawCurrentImageString = content[coordinates.Start..coordinates.End],
                            CurrentImage = parsedImage.Value,
                            AnchorName = scalarImageNode.Anchor.IsEmpty ? new() : new(scalarImageNode.Anchor.ToString())
                        }
                    };
                    images.Add(new(new YamlUpdateLocation
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
