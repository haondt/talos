using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Talos.Core.Extensions;
using Talos.Renovate.Models;
namespace Talos.Renovate.Services
{
    public partial class ImageUpdaterService
    {
        private void LogTrace(ImageUpdateTrace trace)
        {

            var scope = new Dictionary<string, object>
            {
                ["Resolution"] = trace.Resolution,
            };
            if (!string.IsNullOrEmpty(trace.DesiredImage))
                scope["DesiredImage"] = trace.DesiredImage;
            if (trace.DesiredImageCreatedOn.HasValue)
                scope["DesiredImageCreatedOn"] = trace.DesiredImageCreatedOn.Value.UtcTime.ToString("s");
            if (!string.IsNullOrEmpty(trace.CachedImage))
                scope["CachedImage"] = trace.CachedImage;
            if (trace.CachedImageCreatedOn.HasValue)
                scope["CachedImageCreatedOn"] = trace.CachedImageCreatedOn.Value.UtcTime.ToString("s");

            using (_logger.BeginScope(scope))

                _logger.LogInformation("Processed image update {CurrentImage}", trace.CurrentImage);
        }
        private async Task<Optional<ScheduledPush>> ProcessService(ImageUpdateIdentity id, TalosSettings configuration, string image)
        {
            var cached = await TryGetImageUpdateDataAsync(id);
            if (cached.HasValue && cached.Value.Image != image)
            {
                var interactionId = cached.Value.Interaction?.InteractionId;
                if (interactionId != null)
                    await _notificationService.DeleteInteraction(interactionId);
                await ClearImageUpdateDataCacheAsync(id);
                cached = new();
            }

            var target = await SelectUpdateTarget(image, configuration.Bump);
            if (!target.HasValue)
            {
                LogTrace(new ImageUpdateTrace()
                {
                    Resolution = "Skipped due to image already being up to date",
                    CurrentImage = image,
                });
                return new();
            }

            var strategy = target.Value.BumpSize switch
            {
                BumpSize.Major => configuration.Strategy.Major,
                BumpSize.Minor => configuration.Strategy.Minor,
                BumpSize.Patch => configuration.Strategy.Patch,
                BumpSize.Digest => configuration.Strategy.Digest,
                _ => throw new ArgumentException($"Unknown {nameof(BumpStrategy)} {target.Value.BumpSize}")
            };


            switch (strategy)
            {
                case BumpStrategy.Push:
                    {
                        return new ScheduledPush
                        {
                            Update = target.Value,
                            Target = id,
                        };
                    }
                case BumpStrategy.Prompt:
                    {
                        if (cached.HasValue)
                        {
                            if (cached.Value.Interaction != null)
                            {

                                if (target.Value.NewImageCreatedOn <= cached.Value.Interaction.PendingImageCreatedOn)
                                {
                                    LogTrace(new ImageUpdateTrace
                                    {
                                        Resolution = "Skipped due to newer pending version",
                                        DesiredImage = target.Value.NewImage.ToString(),
                                        DesiredImageCreatedOn = target.Value.NewImageCreatedOn,
                                        CurrentImage = image,
                                        CachedImage = cached.Value.Interaction.PendingImage,
                                        CachedImageCreatedOn = cached.Value.Interaction.PendingImageCreatedOn,
                                    });

                                    return new();
                                }

                                if (cached.Value.Interaction.InteractionId != null)
                                {
                                    await _notificationService.DeleteInteraction(cached.Value.Interaction.InteractionId);
                                    cached.Value.Interaction.InteractionId = null;
                                }
                            }
                        }
                        var interactionId = await _notificationService.CreateInteraction(target.Value);
                        var updateData = new ImageUpdateData
                        {
                            Image = image,
                            Interaction = new()
                            {
                                PendingImage = target.Value.NewImage.ToString(),
                                PendingImageCreatedOn = target.Value.NewImageCreatedOn,
                                InteractionId = interactionId,
                            }
                        };

                        await SetImageUpdateDataAsync(id, updateData);
                        LogTrace(new ImageUpdateTrace
                        {
                            Resolution = "Created or replace interaction for pending image",
                            DesiredImage = target.Value.NewImage.ToString(),
                            DesiredImageCreatedOn = target.Value.NewImageCreatedOn,
                            CurrentImage = image,
                            CachedImage = updateData.Interaction.PendingImage,
                            CachedImageCreatedOn = updateData.Interaction.PendingImageCreatedOn,
                        });

                        return new();
                    }
                case BumpStrategy.Notify:
                    {
                        if (cached.HasValue)
                        {
                            if (cached.Value.LastNotified?.CreatedOn >= target.Value.NewImageCreatedOn)
                            {
                                LogTrace(new ImageUpdateTrace
                                {
                                    Resolution = "Skipping notification due to having already notified of newer version",
                                    DesiredImage = target.Value.NewImage.ToString(),
                                    DesiredImageCreatedOn = target.Value.NewImageCreatedOn,
                                    CurrentImage = image,
                                    CachedImage = cached.Value.LastNotified.Image,
                                    CachedImageCreatedOn = cached.Value.LastNotified.CreatedOn
                                });
                                return new();
                            }
                        }
                        if (cached.HasValue)
                        {
                            cached.Value.LastNotified = new()
                            {
                                CreatedOn = target.Value.NewImageCreatedOn,
                                Image = target.Value.NewImage.ToString()
                            };
                            if (cached.Value.LastNotified?.CreatedOn >= target.Value.NewImageCreatedOn)
                            {
                                LogTrace(new ImageUpdateTrace
                                {
                                    Resolution = "Skipping notification due to having already notified of newer version",
                                    DesiredImage = target.Value.NewImage.ToString(),
                                    DesiredImageCreatedOn = target.Value.NewImageCreatedOn,
                                    CurrentImage = image,
                                    CachedImage = cached.Value.LastNotified.Image,
                                    CachedImageCreatedOn = cached.Value.LastNotified.CreatedOn
                                });
                                return new();
                            }
                        }
                        else
                        {
                            cached = new ImageUpdateData()
                            {
                                Image = image,
                                LastNotified = new()
                                {
                                    CreatedOn = target.Value.NewImageCreatedOn,
                                    Image = target.Value.NewImage.ToString()
                                }
                            };
                        }

                        await _notificationService.Notify(target.Value);
                        await SetImageUpdateDataAsync(id, cached.Value!);
                        LogTrace(new ImageUpdateTrace
                        {
                            Resolution = "Notified about available update",
                            DesiredImage = target.Value.NewImage.ToString(),
                            DesiredImageCreatedOn = target.Value.NewImageCreatedOn,
                            CurrentImage = image,
                        });
                        return new();
                    }
                case BumpStrategy.Skip:
                    LogTrace(new ImageUpdateTrace
                    {
                        Resolution = $"Skipped due to {nameof(BumpStrategy)}.{BumpStrategy.Skip} strategy configuration",
                        CurrentImage = image,
                    });
                    return new();
                default:
                    throw new ArgumentException($"Unknown {nameof(BumpStrategy)}: {strategy}");
            }
        }


        public async Task<Optional<ImageUpdate>> SelectUpdateTarget(string image, BumpSize maxBumpSize, bool insertDefaultDomain = true)
        {
            var parsedActiveImage = ImageParser.Parse(image, insertDefaultDomain);
            // use untagged version for more cache hits
            var tags = await _skopeoService.ListTags(parsedActiveImage.Untagged);
            var parsedTags = tags.Select(ImageParser.TryParseTag)
                .Where(q => q.HasValue)
                .Select(q => q.Value!);

            (ParsedImage Image, AbsoluteDateTime ImageCreatedOn, BumpSize Bump) desiredUpdate;

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
                    return new();

                var desiredTag = validTags[0];
                var desiredInspect = await _skopeoService.Inspect($"{parsedActiveImage.Untagged}:{desiredTag}");
                var desiredDigest = desiredInspect.Digest;

                desiredUpdate = (parsedActiveImage with
                {
                    TagAndDigest = new ParsedTagAndDigest(Tag: desiredTag, Digest: desiredDigest)
                }, AbsoluteDateTime.Create(desiredInspect.Created), BumpSize.Digest);
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
                }, AbsoluteDateTime.Create(validTagInspect.Created), BumpSize.Digest);
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
                }, AbsoluteDateTime.Create(highestValidTagInspect.Created), highestValidVersion.BumpSize);
            }

            return new(new(parsedActiveImage, desiredUpdate.Image, desiredUpdate.ImageCreatedOn, desiredUpdate.Bump));
        }

        private async Task CompletePushAsync(ScheduledPush push)
        {
            var cached = (await TryGetImageUpdateDataAsync(push.Target))
                .As(c =>
                {
                    c.Image = push.Update.NewImage.ToString();
                    return c;
                }).Or(new ImageUpdateData()
                {
                    Image = push.Update.NewImage.ToString()
                });

            if (cached.LastNotified != null)
                if (cached.LastNotified.CreatedOn <= push.Update.NewImageCreatedOn)
                    cached.LastNotified = null;

            if (cached.Interaction != null)
            {
                if (cached.Interaction.PendingImageCreatedOn <= push.Update.NewImageCreatedOn)
                {
                    if (cached.Interaction.InteractionId != null)
                        await _notificationService.DeleteInteraction(cached.Interaction.InteractionId);
                    cached.Interaction = null;
                }
            }

            await SetImageUpdateDataAsync(push.Target, cached);
        }
    }
}
