using Haondt.Core.Models;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Text.RegularExpressions;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class DockerfileService
    {
        public static DetailedResult<(string NewFileContents, string NewLineContents), string> SetFromImage(string fileContents, int line, string image)
        {
            var lines = fileContents.Split(Environment.NewLine);
            var lineText = lines[line];
            var extractionResult = TryExtractFromImage(lineText);
            if (!extractionResult.IsSuccessful)
                return new($"Couldn't extract image from line {lineText}");

            var (head, previousImage, tail) = extractionResult.Value;

            lines[line] = head + image + tail;
            return new((string.Join(Environment.NewLine, lines), lines[line]));
        }

        private static List<(string AbsolutePath, string RelativePath)> GetDockerfileTargets(
            string repositoryDirectoryPath, RepositoryConfiguration repository)
        {
            var matcher = new Matcher();
            matcher.AddExclude("**/.git/**");
            if (repository.Glob.Dockerfile?.IncludeGlobs != null)
                matcher.AddIncludePatterns(repository.Glob.Dockerfile.IncludeGlobs);
            else
                matcher.AddInclude("**/Dockerfile");
            if (repository.Glob.Dockerfile?.ExcludeGlobs != null)
                matcher.AddExcludePatterns(repository.Glob.Dockerfile.ExcludeGlobs);

            var result = matcher.Execute(
                new DirectoryInfoWrapper(
                    new DirectoryInfo(repositoryDirectoryPath)));
            var files = new List<(string, string)>();
            if (result.HasMatches)
                files = result.Files.Select(f => (Path.Join(repositoryDirectoryPath, f.Path), f.Path)).ToList();

            return files;
        }

        private static Result<(string Head, string Image, string Tail)> TryExtractFromImage(string line)
        {
            var pattern = @"^(?<head>(?i)FROM(?-i)\s+)(?<image>\S+)(?<tail>(?:\s+(?i)AS(?-i)\s+\S+)?\s*)$";
            var match = Regex.Match(line, pattern);
            if (!match.Success)
                return Result<(string, string, string)>.Failure;
            return (match.Groups["head"].Value, match.Groups["image"].Value, match.Groups["tail"].Value);
        }

        private static DetailedResult<TalosSettings, string> ParseTalosLines(List<string> talosLines, string? shortFormValue)
        {
            if (shortFormValue != null)
            {
                if (talosLines.Count > 0)
                    return new($"Unable to parse short form {shortFormValue} alongside other talos lines");
                return TalosSettings.ParseShortForm(shortFormValue);
            }

            const string talosParameterPattern = @"(?<key>[\w.]+)=(?<value>[\w.]+)";
            var talosSettings = new TalosSettings();
            foreach (var talosLine in talosLines)
            {
                var parameterMatches = Regex.Matches(talosLine, talosParameterPattern);
                var syncSettings = new Dictionary<string, string>();
                foreach (Match parameterMatch in parameterMatches)
                {
                    var key = parameterMatch.Groups["key"].Value.Trim().ToLower();
                    var value = parameterMatch.Groups["value"].Value;
                    switch (key)
                    {
                        case "skip":
                            if (!bool.TryParse(value, out var skip))
                                return new($"Failed to parse skip value {value} as {typeof(bool)}.");
                            talosSettings.Skip = skip;
                            break;
                        case "bump":
                            if (!Enum.TryParse<BumpSize>(value, out var bump))
                                return new($"Failed to parse bump value {value} as {typeof(BumpSize)}.");
                            talosSettings.Bump = bump;
                            break;
                        case "strategy.digest":
                            if (!Enum.TryParse<BumpStrategy>(value, out var digest))
                                return new($"Failed to parse digest value {value} as {typeof(BumpStrategy)}.");
                            talosSettings.Strategy.Digest = digest;
                            break;
                        case "strategy.patch":
                            if (!Enum.TryParse<BumpStrategy>(value, out var patch))
                                return new($"Failed to parse patch value {value} as {typeof(BumpStrategy)}.");
                            talosSettings.Strategy.Patch = patch;
                            break;
                        case "strategy.minor":
                            if (!Enum.TryParse<BumpStrategy>(value, out var minor))
                                return new($"Failed to parse minor value {value} as {typeof(BumpStrategy)}.");
                            talosSettings.Strategy.Minor = minor;
                            break;
                        case "strategy.major":
                            if (!Enum.TryParse<BumpStrategy>(value, out var major))
                                return new($"Failed to parse major value {value} as {typeof(BumpStrategy)}.");
                            talosSettings.Strategy.Major = major;
                            break;
                        case "sync.role":
                        case "sync.group":
                        case "sync.id":
                        case "sync.children":
                        case "sync.components":
                            syncSettings[key] = value;
                            break;
                    }
                }

                if (syncSettings.Count > 0)
                {
                    if (!syncSettings.TryGetValue("sync.role", out var roleString))
                        return new($"Sync settings were provided, but did not include role.");
                    if (!Enum.TryParse<SyncRole>(roleString, out var role))
                        return new($"Failed to parse sync.role {roleString} as {typeof(SyncRole)}.");
                    if (!syncSettings.TryGetValue("sync.group", out var group))
                        return new($"Sync settings were provided, but did not include group.");
                    if (string.IsNullOrEmpty(group))
                        return new("sync.group cannot be empty.");

                    if (!syncSettings.TryGetValue("sync.id", out var id))
                        return new($"Sync settings were provided, but did not include id.");
                    if (string.IsNullOrEmpty(id))
                        return new("sync.id cannot be empty.");

                    talosSettings.Sync = new()
                    {
                        Id = id,
                        Group = group,
                        Role = role
                    };


                    if (role == SyncRole.Parent)
                    {
                        if (syncSettings.TryGetValue("sync.digest", out var digestString))
                            if (!bool.TryParse(digestString, out var digest))
                                return new($"Unable to parse sync.digest {digestString} as bool");
                            else
                                talosSettings.Sync.Digest = digest;

                        if (syncSettings.TryGetValue("sync.children", out var childIds))
                            talosSettings.Sync.Children = childIds.Split(',')
                                .Where(q => !string.IsNullOrEmpty(q))
                                .ToList();
                    }
                }
            }

            return new(talosSettings);
        }

        public static List<DetailedResult<IUpdateLocation, string>> ExtractUpdateTargets(RepositoryConfiguration repository, string clonedRepositoryDirectory, IImageParser imageParser)
        {
            var images = new List<DetailedResult<IUpdateLocation, string>>();
            const string talosLinePattern = @"^#\s+(?i)!talos(?-i)(?:\s+(?:[\w.]+=[\w.]+))*\s*$";
            const string talosShortFormPattern = @"^#\s+(?i)!talos:\s+(?<value>\S+)\s*$";

            foreach (var (absoluteFilePath, relativeFilePath) in GetDockerfileTargets(clonedRepositoryDirectory, repository))
            {
                var content = File.ReadAllText(absoluteFilePath);
                var lines = content.Split(Environment.NewLine);

                var fromLines = new Dictionary<int, string>();

                // Look for FROM instruction
                for (int i = 0; i < lines.Length; i++)
                {
                    var matchResult = TryExtractFromImage(lines[i]);
                    if (!matchResult.IsSuccessful)
                        continue;
                    fromLines[i] = matchResult.Value.Image;
                }

                foreach (var (line, image) in fromLines)
                {
                    var coordinates = new DockerfileUpdateLocationCoordinates { Line = line, RelativeFilePath = relativeFilePath };

                    var talosLines = new List<string>();
                    var shortFormValues = new List<string>();
                    for (int talosLineIndex = line + 1; talosLineIndex < lines.Length; talosLineIndex++)
                    {
                        var talosLine = lines[talosLineIndex];

                        var shortFormMatch = Regex.Match(talosLine, talosShortFormPattern);
                        if (shortFormMatch.Success)
                        {
                            shortFormValues.Add(shortFormMatch.Groups["value"].Value);
                            continue;
                        }

                        var talosMatch = Regex.Match(talosLine, talosLinePattern);
                        if (!talosMatch.Success)
                            break;
                        talosLines.Add(talosLine);
                    }

                    if (talosLines.Count == 0 && shortFormValues.Count == 0)
                    {
                        images.Add(new($"{coordinates}: missing talos extension"));
                        continue;
                    }

                    if (shortFormValues.Count > 1)
                    {
                        images.Add(new($"{coordinates}: found multiple short form values"));
                        continue;
                    }

                    var talosSettings = ParseTalosLines(talosLines, shortFormValues.SingleOrDefault());
                    if (!talosSettings.IsSuccessful)
                    {
                        images.Add(new($"{coordinates}:  {talosSettings.Reason}"));
                        continue;
                    }

                    var parsedImage = imageParser.TryParse(image);
                    if (!parsedImage.HasValue)
                    {
                        images.Add(new($"{coordinates}: couldn't parse image {image}"));
                        continue;
                    }

                    var lineHash = HashUtils.ComputeSha256Hash(lines[line]);
                    var state = new DockerfileUpdateLocationState
                    {
                        Configuration = talosSettings.Value,
                        Snapshot = new()
                        {
                            CurrentImage = parsedImage.Value,
                            LineHash = lineHash
                        }
                    };


                    images.Add(new(new DockerfileUpdateLocation()
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
