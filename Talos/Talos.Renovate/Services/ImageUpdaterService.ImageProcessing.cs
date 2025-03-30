using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Talos.Core.Extensions;
using Talos.Renovate.Models;
namespace Talos.Renovate.Services
{
    public partial class ImageUpdaterService
    {
        public static Dictionary<string, object?> FlattenObject(object obj)
        {

            var json = JsonConvert.SerializeObject(obj, SerializationConstants.TraceSerializerSettings);
            var jObject = JObject.Parse(json);
            return jObject.Descendants()
                .OfType<JValue>()
                .ToDictionary(
                    jv => jv.Path.Replace("[", ".").Replace("]", ""),
                    jv => jv.Value);
        }
        private void LogTrace(ImageUpdateTrace trace)
        {
            using (_logger.BeginScope(FlattenObject(trace)))
                _logger.LogInformation("Processed image update {CurrentImage}", trace.Identity);
        }
        private async Task<Optional<ScheduledPushWithIdentity>> ProcessServiceAsync(UpdateIdentity id, IUpdateLocation location)
        {
            using var span = tracer.StartSpan($"{nameof(ProcessServiceAsync)}");
            span.SetAttribute(nameof(UpdateIdentity), id.ToString());
            var cached = await updateDataRepository.TryGetImageUpdateDataAsync(id);

            // if the location snapshot is different than expected, drop the cache
            if (cached.IsSuccessful && !location.State.Snapshot.IsEquivalentTo(cached.Value.LastKnownSnapshot))
            {
                var interactionId = cached.Value.Interaction.As(q => q.InteractionId);
                if (interactionId.HasValue && interactionId.Value.HasValue)
                    await _notificationService.DeleteInteraction(interactionId.Value.Value);
                await updateDataRepository.ClearImageUpdateDataCacheAsync(id);
                cached = new();
            }


            var push = await location.CreateScheduledPushAsync(this);
            if (!push.HasValue)
            {
                LogTrace(new ImageUpdateTrace()
                {
                    Resolution = "Skipped due to image already being up to date",
                    Push = push,
                    Identity = id,
                    Cached = cached.AsOptional(),
                });
                return new();
            }

            var idPush = new ScheduledPushWithIdentity(id, push.Value);

            var strategy = push.Value.BumpSize switch
            {
                BumpSize.Major => location.State.Configuration.Strategy.Major,
                BumpSize.Minor => location.State.Configuration.Strategy.Minor,
                BumpSize.Patch => location.State.Configuration.Strategy.Patch,
                BumpSize.Digest => location.State.Configuration.Strategy.Digest,
                _ => throw new ArgumentException($"Unknown {nameof(BumpStrategy)} {push.Value.BumpSize}")
            };


            switch (strategy)
            {
                case BumpStrategy.Push:
                    return new(new(id, push.Value));
                case BumpStrategy.Prompt:
                    {
                        if (cached.IsSuccessful)
                        {
                            if (cached.Value.Interaction.HasValue)
                            {
                                if (!push.Value.IsNewerThan(cached.Value.Interaction.Value.PendingPush))
                                {

                                    LogTrace(new ImageUpdateTrace
                                    {
                                        Resolution = "Skipped due to newer pending version",
                                        Push = push,
                                        Identity = id,
                                        Cached = cached.AsOptional(),
                                    });

                                    return new();
                                }


                                if (cached.Value.Interaction.Value.InteractionId.HasValue)
                                {
                                    await _notificationService.DeleteInteraction(cached.Value.Interaction.Value.InteractionId.Value!);
                                    cached.Value.Interaction.Value.InteractionId = new();
                                }
                            }
                        }
                        var interactionId = await _notificationService.CreateInteractionAsync(idPush);
                        var updateData = new ImageUpdateData
                        {
                            LastKnownSnapshot = location.State.Snapshot,
                            Interaction = new(new()
                            {
                                PendingPush = push.Value,
                                InteractionId = interactionId,
                            })
                        };

                        await updateDataRepository.SetImageUpdateDataAsync(id, updateData);
                        LogTrace(new ImageUpdateTrace
                        {
                            Resolution = "Created or replace interaction for pending image",
                            Push = push,
                            Identity = id,
                            Cached = cached.AsOptional(),
                        });

                        return new();
                    }
                case BumpStrategy.Notify:
                    {
                        if (cached.IsSuccessful)
                        {
                            if (cached.Value.LastNotified.HasValue && !push.Value.IsNewerThan(cached.Value.LastNotified.Value))
                            {
                                LogTrace(new ImageUpdateTrace
                                {
                                    Resolution = "Skipping notification due to having already notified of newer version",
                                    Push = push,
                                    Identity = id,
                                    Cached = cached.AsOptional(),
                                });
                                return new();
                            }

                            cached.Value.LastNotified = push;
                        }
                        else
                        {
                            cached = new(new()
                            {
                                LastKnownSnapshot = location.State.Snapshot,
                                LastNotified = push
                            });
                        }

                        await _notificationService.Notify(idPush);
                        await updateDataRepository.SetImageUpdateDataAsync(id, cached.Value!);
                        LogTrace(new ImageUpdateTrace
                        {
                            Resolution = "Notified about available update",
                            Push = push,
                            Identity = id,
                            Cached = cached.AsOptional(),
                        });
                        return new();
                    }
                case BumpStrategy.Skip:
                    LogTrace(new ImageUpdateTrace
                    {
                        Resolution = $"Skipped due to {nameof(BumpStrategy)}.{BumpStrategy.Skip} strategy configuration",
                        Push = push,
                        Identity = id,
                        Cached = cached.AsOptional(),
                    });
                    return new();
                default:
                    throw new ArgumentException($"Unknown {nameof(BumpStrategy)}: {strategy}");
            }
        }

        /// <summary>
        /// Get the most recent digest for the <paramref name="image"/>, with its tag replaced by <paramref name="tag"/>.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public async Task<(string Digest, AbsoluteDateTime Created)> GetDigestAsync(ParsedImage image, ParsedTag tag)
        {
            var inspect = await _skopeoService.Inspect($"{image.Untagged}:{tag}");
            return (inspect.Digest, AbsoluteDateTime.Create(inspect.Created));
        }

        public async Task<List<ParsedTag>> GetSortedCandidateTagsAsync(ParsedImage parsedActiveImage, BumpSize maxBumpSize, bool insertDefaultDomain = true)
        {
            // use untagged version for more cache hits
            var tags = await _skopeoService.ListTags(parsedActiveImage.Untagged);
            var parsedTags = tags.Select(_imageParser.TryParseTag)
                .Where(q => q.HasValue)
                .Select(q => q.Value!);

            if (!parsedActiveImage.TagAndDigest.HasValue)
            {
                // for missing tags, we will add the latest tag and a digest
                // -> latest@sha256:123

                var validTags = parsedTags
                    .Where(t => t.Version.Is<string>(out var release)
                        && release == options.Value.DefaultRelease
                        && !t.Variant.HasValue)
                    .ToList();

                if (validTags.Count == 0)
                    return [];

                var desiredTag = validTags[0];
                return [desiredTag];
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
                    return [];

                var validTag = validTagsList[0];
                return [validTag];
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
                    return [];



                var sorted = validTags.OrderByDescending(q => q.SemanticVersion.Major)
                    .ThenByDescending(q => q.SemanticVersion.Minor.Or(-1))
                    .ThenByDescending(q => q.SemanticVersion.Patch.Or(-1));

                return sorted.Select(q => new ParsedTag(Version: q.SemanticVersion, Variant: parsedActiveImage.TagAndDigest.Value.Tag.Variant)).ToList();
            }
        }

        public Optional<BumpSize> IsUpgrade(Optional<ParsedTagAndDigest> from, ParsedTag toTag, string toDigest)
        {
            if (!from.HasValue)
                // for missing tags, we will add the latest tag and a digest
                // -> latest@sha256:123
                return BumpSize.Digest;
            else if (from.Value.Tag.Version.Is<string>(out var release))
            {
                // with releases, we will only add the digest if it dne, or update it if there is a newer one
                // latest -> latest@sha256:123
                // latest@sha256:123 -> latest@sha256:abc
                // we must also match the variant
                // latest-alpine -> latest-alpine@sha256:abc
                if (!from.Value.Digest.HasValue)
                    return BumpSize.Digest;
                if (from.Value.Digest.Value != toDigest)
                    return BumpSize.Digest;
                return new();
            }
            else
            {
                // with semver, we will find all the versions that keep the same precision
                // (e.g. we wont do v1.2 -> v2 or v1.2 -> v2.0.0, but we will do v1.2 -> v2.0)
                // then filter them by the configured bump level
                // we must also match the variant
                // v1.2.3-alpine@sha256:abc -> v1.3.1-alpine@sha256:abc
                var size = SemanticVersion.Compare(from.Value.Tag.Version.Cast<SemanticVersion>(), toTag.Version.Cast<SemanticVersion>());
                BumpSize bumpSize;

                switch (size)
                {
                    case SemanticVersionSize.Major:
                        bumpSize = BumpSize.Major;
                        break;
                    case SemanticVersionSize.Minor:
                        bumpSize = BumpSize.Minor;
                        break;
                    case SemanticVersionSize.Patch:
                        bumpSize = BumpSize.Patch;
                        break;
                    case SemanticVersionSize.Equal:
                        bumpSize = BumpSize.Digest;
                        break;
                    case SemanticVersionSize.PrecisionMismatch:
                    case SemanticVersionSize.Downgrade:
                    default:
                        return new();
                }

                if (!from.Value.Digest.HasValue)
                    return bumpSize;
                if (from.Value.Digest.Value != toDigest)
                    return bumpSize;
                return new();
            }
        }

        private async Task CompletePushAsync(ScheduledPushWithIdentity push, IUpdateLocationSnapshot snapshot)
        {
            using var span = tracer.StartSpan(nameof(CompletePushAsync));
            span.SetAttribute(nameof(UpdateIdentity), push.Identity.ToString());
            var cachedResult = await updateDataRepository.TryGetImageUpdateDataAsync(push.Identity);
            ImageUpdateData cached;
            if (cachedResult.IsSuccessful)
            {
                cachedResult.Value.LastKnownSnapshot = snapshot;
                cached = cachedResult.Value;
            }
            else
                cached = new()
                {
                    LastKnownSnapshot = snapshot
                };

            if (cached.LastNotified.HasValue)
                if (!cached.LastNotified.Value.IsNewerThan(push.Push))
                    cached.LastNotified = new();

            if (cached.Interaction.HasValue)
            {
                if (!cached.Interaction.Value.PendingPush.IsNewerThan(push.Push))
                {
                    if (cached.Interaction.Value.InteractionId.HasValue)
                        await _notificationService.DeleteInteraction(cached.Interaction.Value.InteractionId.Value);
                    cached.Interaction = new();
                }
            }

            await updateDataRepository.SetImageUpdateDataAsync(push.Identity, cached);
        }
    }
}
