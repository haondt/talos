using Haondt.Core.Extensions;
using Haondt.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Extensions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class ImageUpdaterService(
        IOptions<ImageUpdateSettings> updateOptions,
        IOptions<ImageUpdaterSettings> options,
        ILogger<ImageUpdaterService> _logger,
        INotificationService _notificationService,
        ISkopeoService _skopeoService,
        IRedisProvider redisProvider) : IImageUpdaterService
    {
        private static string DEFAULT_RELEASE = "latest";

        private readonly record struct ScheduledPush(
            ImageUpdateIdentity Target,
            string NewTag)
        {

        }

        private readonly record struct ImageUpdateIdentity(
            string GitRemoteUrl,
            string RelativeFilePath,
            string YamlPath)
        {
            public override string ToString()
            {
                return $"{GitRemoteUrl}:{RelativeFilePath}:{YamlPath}";
            }
        }

        private readonly ImageUpdateSettings _updateSettings = updateOptions.Value;
        private readonly IDatabase _redis = redisProvider.GetDatabase(options.Value.RedisDatabase);
        public async Task RunAsync()
        {
            _logger.LogInformation("Starting image update run.");

            var tasks = _updateSettings.Repositories.Select(async r =>
            {
                try
                {
                    var host = _updateSettings.Hosts[r.Host];
                    await RunAsync(host, r);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Image update on repository {repositoryUrl} failed due to exception: {exceptionMessage}", r.Url, ex.Message);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation("Image update run complete.");
        }


        private async Task RunAsync(HostConfiguration host, RepositoryConfiguration repository)
        {
            using var repoDir = CloneRepository(host, repository);
            var processingTasks = ExtractUpdateTargets(repository, repoDir.Path)
                .Select(q =>
                {
                    try
                    {
                        return ProcessService(q.Id, q.Configuration, q.Image);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Processing update for image {image} failed due to exception: {exceptionMessage}", q.Id, ex.Message);
                        return Task.FromResult(new Optional<ScheduledPush>());
                    }
                });

            var scheduledPushes = (await Task.WhenAll(processingTasks))
                .Where(p => p.HasValue)
                .Select(p => p.Value);

        }

        private async Task<Optional<ScheduledPush>> ProcessService(ImageUpdateIdentity id, TalosSettings configuration, string image)
        {
            var cached = await TryGetImageUpdateDataAsync(id);
            if (cached.HasValue && cached.Value.Image != image)
            {
                var interactionId = cached.Value.Interaction?.InteractionId;
                if (interactionId != null)
                    await _notificationService.DeleteNotification(interactionId);
                await ClearCacheAsync(id);
            }

            var target = await SelectUpdateTarget(image, configuration.Bump);

            throw new NotImplementedException();

        }

        public async Task<Optional<(string DesiredImage, BumpSize BumpSize)>> SelectUpdateTarget(string image, BumpSize maxBumpSize)
        {
            var parsedActiveImage = ImageParser.Parse(image);
            // use untagged version for more cache hits
            var tags = await _skopeoService.ListTags(parsedActiveImage.Untagged);
            var parsedTags = tags.Select(ImageParser.TryParseTag)
                .Where(q => q.HasValue)
                .Select(q => q.Value);

            (ParsedImage Image, BumpSize Bump) desiredUpdate;

            if (!parsedActiveImage.TagAndDigest.HasValue)
            {
                // for missing tags, we will add the latest tag and a digest
                // -> latest@sha256:123

                var validTags = parsedTags
                    .Where(t => t.Version.Is<string>(out var release)
                        && release == DEFAULT_RELEASE
                        && !t.Variant.HasValue)
                    .ToList();

                if (validTags.Count == 0)
                    return new();

                var desiredTag = validTags[0];
                var desiredInspect = await _skopeoService.Inspect($"{parsedActiveImage.Untagged}:{desiredTag}");
                var desiredDigest = desiredInspect.Digest;

                desiredUpdate = (parsedActiveImage with
                {
                    TagAndDigest = new ParsedTagAndDigest(Tag: desiredTag, Digest: desiredDigest)
                }, BumpSize.Digest);
            }
            else if (parsedActiveImage.TagAndDigest.Value.Tag.Version.Is<string>(out var release))
            {
                // with releases, we will only add the digest if it dne, or update it if there is a newer one
                // latest -> latest@sha256:123
                // latest@sha256:123 -> latest@sha256:abc
                // we must also match the variant
                // latest-alpine -> latest-alpine@sha256:abc

                var validTags = parsedTags
                    .Where(t => t.Version.Is<string>(out var r)
                        && r == release
                        && t.Variant.IsEquivalentTo(parsedActiveImage.TagAndDigest.Value.Tag.Variant));
                var validTagsList = validTags.ToList();

                if (validTagsList.Count == 0)
                    return new();

                var validTag = validTagsList[0];
                var validTagInspect = await _skopeoService.Inspect($"{parsedActiveImage.Untagged}:{validTag}");
                var desiredDigest = validTagInspect.Digest;

                if (parsedActiveImage.TagAndDigest.Value.Digest.HasValue)
                    if (parsedActiveImage.TagAndDigest.Value.Digest.Value == desiredDigest)
                        return new();

                desiredUpdate = (parsedActiveImage with
                {
                    TagAndDigest = new ParsedTagAndDigest(validTag, desiredDigest)
                }, BumpSize.Digest);
            }
            else
            {
                // with semver, we will find all the versions that keep the same precision
                // (e.g. we wont do v1.2 -> v2 or v1.2 -> v2.0.0, but we will do v1.2 -> v2.0)
                // then filter them by the configured bump level
                // we must also match the variant
                // v1.2.3-alpine@sha256:abc -> v1.3.1-alpine@sha256:abc

                var activeVersion = parsedActiveImage.TagAndDigest.Value.Tag.Version.Cast<SemanticVersion>();

                var validTags = new List<(SemanticVersion SemanticVersion, BumpSize BumpSize)>();
                foreach (var parsedTag in parsedTags)
                {
                    if (!parsedTag.Version.Is<SemanticVersion>(out var sv))
                        continue;
                    if (!sv.VersionPrefix.IsEquivalentTo(activeVersion.VersionPrefix))
                        continue;
                    if (!parsedTag.Variant.IsEquivalentTo(parsedActiveImage.TagAndDigest.Value.Tag.Variant))
                        continue;

                    var size = SemanticVersion.Compare(activeVersion, sv);
                    BumpSize bumpSize;

                    switch (size)
                    {
                        case SemanticVersionSize.Major:
                            if (maxBumpSize < BumpSize.Major)
                                continue;
                            bumpSize = BumpSize.Major;
                            break;
                        case SemanticVersionSize.Minor:
                            if (maxBumpSize < BumpSize.Minor)
                                continue;
                            bumpSize = BumpSize.Minor;
                            break;
                        case SemanticVersionSize.Patch:
                            if (maxBumpSize < BumpSize.Patch)
                                continue;
                            bumpSize = BumpSize.Patch;
                            break;
                        case SemanticVersionSize.Equal:
                            bumpSize = BumpSize.Digest;
                            break;
                        case SemanticVersionSize.PrecisionMismatch:
                        case SemanticVersionSize.Downgrade:
                        default:
                            continue;
                    }

                    validTags.Add((sv, bumpSize));
                }

                if (validTags.Count == 0)
                    return new();

                var highestValidVersion = validTags.OrderByDescending(q => q.SemanticVersion.Major)
                    .ThenByDescending(q => q.SemanticVersion.Minor.Or(-1))
                    .ThenByDescending(q => q.SemanticVersion.Patch.Or(-1))
                    .First();

                var highestValidTag = new ParsedTag(Version: highestValidVersion.SemanticVersion, Variant: parsedActiveImage.TagAndDigest.Value.Tag.Variant);
                var highestValidTagInspect = await _skopeoService.Inspect($"{parsedActiveImage.Untagged}:{highestValidTag}");
                var highestValidTagDigest = highestValidTagInspect.Digest;

                if (parsedActiveImage.TagAndDigest.Value.Digest.HasValue)
                    if (parsedActiveImage.TagAndDigest.Value.Digest.Value == highestValidTagDigest)
                        return new();

                desiredUpdate = (parsedActiveImage with
                {
                    TagAndDigest = new ParsedTagAndDigest(Tag: highestValidTag, Digest: highestValidTagDigest)
                }, BumpSize.Digest);
            }

            return (desiredUpdate.Image.ToString(), desiredUpdate.Bump);
        }

        private Task<bool> ClearCacheAsync(ImageUpdateIdentity id)
        {
            return _redis.KeyDeleteAsync(id.ToString());
        }

        private async Task<Optional<ImageUpdateData>> TryGetImageUpdateDataAsync(ImageUpdateIdentity id)
        {
            var cachedResponse = await _redis.StringGetAsync(id.ToString());
            if (cachedResponse.IsNull)
                return new();
            var deserialized = JsonConvert.DeserializeObject<ImageUpdateData>(cachedResponse.ToString(), SerializationConstants.SerializerSettings);
            if (deserialized == null)
            {
                _logger.LogWarning("Failed to parse stored json for image update data {image}.", id);
                return new();
            }

            return deserialized;
        }

        private List<(ImageUpdateIdentity Id, TalosSettings Configuration, string Image)> ExtractUpdateTargets(RepositoryConfiguration repository, string clonedRepositoryDirectory)
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
                    var id = new ImageUpdateIdentity(normalizedUrl, relativeFilePath, $"services.{service.Key}");

                    if (string.IsNullOrEmpty(service.Value.Image))
                    {
                        _logger.LogWarning("Image {image} does not have an image tag, skipping...", id);
                        continue;
                    }

                    if (service.Value.XTalos == null)
                    {
                        _logger.LogWarning("Image {image} does not have talos extension, skipping...", id);
                        continue;
                    }

                    if (service.Value.XTalos.Skip)
                    {
                        _logger.LogInformation("Skipping image {image} due to configured skip...", id);
                        continue;
                    }

                    images.Add((
                        new(normalizedUrl, relativeFilePath, $"services.{service.Key}"),
                        service.Value.XTalos,
                        service.Value.Image));
                }
            }

            return images;

        }

        private TemporaryDirectory CloneRepository(HostConfiguration host, RepositoryConfiguration repository)
        {
            var type = host.Type;
            if (host.Type != HostType.GitLab)
                throw new NotSupportedException($"Unable to handle hosts of type {host.Type}");

            var cloneOptions = new CloneOptions();
            if (!string.IsNullOrEmpty(repository.Branch))
                cloneOptions.BranchName = repository.Branch;
            cloneOptions.FetchOptions.Depth = 1;

            if (string.IsNullOrEmpty(host.Token))
                throw new InvalidOperationException("Token is required for GitLab host");
            cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cred) =>
                new UsernamePasswordCredentials { Username = host.Username ?? "oauth2", Password = host.Token };

            var repoDir = new TemporaryDirectory();
            Repository.Clone(repository.Url, repoDir.Path, cloneOptions);

            return repoDir;
        }

        private List<(string AbsolutePath, string RelativePath)> GetTargetFiles(string repositoryDirectoryPath, RepositoryConfiguration repository)
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
    }
}
