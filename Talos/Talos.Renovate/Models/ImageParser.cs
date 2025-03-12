using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Models
{
    public class ImageParser : IImageParser
    {
        private const string DEFAULT_DOMAIN_NAMESPACE = "library";
        private const string DEFAULT_DOMAIN = "docker.io";
        private readonly Regex _imageRegex;
        private readonly Regex _tagAndDigestRegex;
        private readonly Regex _tagRegex;
        private readonly ILogger<ImageParser> _logger;

        public ImageParser(IOptions<ImageParserSettings> options, ILogger<ImageParser> logger)
        {
            var settings = options.Value;
            var tagPattern = $@"(?<versionprefix>v)?(?:(?:(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<patch>\d+))?)?)|(?<release>{string.Join('|', options.Value.ValidReleases.Select(Regex.Escape))}))(?:-(?<variant>\w+))?";
            var tagAndDigestPattern = $@"(?<tag>{tagPattern})(?:@(?<digest>sha\d+:[a-f0-9]+))?";
            var imagePattern = $@"(?<untagged>(?:(?<domain>[\w.\-_]+\.[\w.\-_]+(?::\d+)?)/)?(?:(?<namespace>(?:[\w.\-_]+)(?:/[\w.\-_]+)*)/)?(?<name>[a-z0-9.\-_]+))(?::(?<taganddigest>{tagAndDigestPattern}))?";
            _imageRegex = new($"^{imagePattern}$");
            _tagAndDigestRegex = new($"^{tagAndDigestPattern}$");
            _tagRegex = new($"^{tagPattern}$");
            _logger = logger;
        }

        public Optional<ParsedImage> TryParse(string image, bool insertDefaultDomain = false)
        {
            var match = _imageRegex.Match(image);

            if (!match.Success)
                return new();

            var domain = TryExtractNonEmptyGroup(match, "domain");
            var @namespace = TryExtractNonEmptyGroup(match, "namespace");
            var name = TryExtractNonEmptyGroup(match, "name");
            var untagged = TryExtractNonEmptyGroup(match, "untagged");
            if (!name.HasValue)
                return new();
            if (!untagged.HasValue)
                return new();

            if (insertDefaultDomain && !domain.HasValue)
            {
                domain = DEFAULT_DOMAIN;
                if (!@namespace.HasValue)
                    @namespace = DEFAULT_DOMAIN_NAMESPACE;
            }

            var tagAndDigest = TryParseTagAndDigest(match);

            return new(new(
                Domain: domain,
                Namespace: @namespace,
                Name: name.Value,
                Untagged: untagged.Value,
                TagAndDigest: tagAndDigest));
        }

        public Optional<ParsedTagAndDigest> TryParseTagAndDigest(string tagAndDigest)
        {
            var match = _tagAndDigestRegex.Match(tagAndDigest);
            if (!match.Success)
                return new();
            return TryParseTagAndDigest(match);
        }
        private Optional<ParsedTagAndDigest> TryParseTagAndDigest(Match match)
        {
            var digest = TryExtractNonEmptyGroup(match, "digest");
            var tag = TryParseTag(match);
            if (!tag.HasValue)
                return new();

            return new(new(tag.Value, digest));
        }

        public Optional<ParsedTag> TryParseTag(string tag)
        {
            var match = _tagRegex.Match(tag);
            if (!match.Success)
                return new();
            return TryParseTag(match);
        }

        public Optional<ParsedTag> TryParseTag(Match match)
        {
            Union<SemanticVersion, string> version;
            var majorString = TryExtractNonEmptyGroup(match, "major");
            if (majorString.HasValue)
            {
                try
                {
                    version = new(new SemanticVersion(
                        VersionPrefix: TryExtractNonEmptyGroup(match, "versionprefix"),
                        Major: int.Parse(majorString.Value),
                        Minor: TryExtractNonEmptyGroup(match, "minor").As(int.Parse),
                        Patch: TryExtractNonEmptyGroup(match, "patch").As(int.Parse)));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Ran into exception while parsing image tag match {match}", match.Value);
                    return new();
                }
            }
            else if (TryExtractNonEmptyGroup(match, "release").TryGetValue(out var release))
            {
                version = new(release);
            }
            else
            {
                return new();
            }

            var variant = TryExtractNonEmptyGroup(match, "variant");
            return new(new(version, variant));
        }




        public ParsedImage Parse(string image, bool insertDefaultDomain = false)
        {
            var parsed = TryParse(image, insertDefaultDomain);
            if (parsed.HasValue)
                return parsed.Value;
            throw new ArgumentException($"Unable to parse image {image}");
        }

        private static Optional<string> TryExtractNonEmptyGroup(Match match, string key)
        {
            if (!match.Groups.TryGetValue(key, out var group))
                return new();

            if (!group.Success || string.IsNullOrEmpty(group.Value))
                return new();

            return group.Value;
        }

    }

    public record ParsedImage(
        string Name,
        string Untagged,
        Optional<string> Domain = default,
        Optional<string> Namespace = default,
        Optional<ParsedTagAndDigest> TagAndDigest = default)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Domain.HasValue)
                sb.Append(Domain.Value + '/');
            if (Namespace.HasValue)
                sb.Append(Namespace.Value + '/');
            sb.Append(Name);
            if (TagAndDigest.HasValue)
                sb.Append(':' + TagAndDigest.Value.ToString());
            return sb.ToString();
        }

        public string ToShortString()
        {
            var sb = new StringBuilder(Name);
            if (!TagAndDigest.HasValue)
                return sb.ToString();
            sb.Append(':' + TagAndDigest.Value.Tag.ToString());
            if (TagAndDigest.Value.Digest.HasValue)
            {
                if (TagAndDigest.Value.Digest.Value.StartsWith("sha256:"))
                    sb.Append(string.Concat("@", TagAndDigest.Value.Digest.Value.AsSpan("sha256:".Length, 8)));
                else
                    sb.Append(string.Concat("@", TagAndDigest.Value.Digest.Value.AsSpan(0, 8)));
            }
            return sb.ToString();
        }
    }

    public readonly record struct ParsedTagAndDigest(
        ParsedTag Tag,
        Optional<string> Digest = default)
    {

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Tag.ToString());
            if (Digest.HasValue)
                sb.Append($"@{Digest.Value}");
            return sb.ToString();
        }
    }
    public record ParsedTag(
        Union<SemanticVersion, string> Version,
        Optional<string> Variant = default)
    {

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Version.Unwrap().ToString());
            if (Variant.HasValue)
                sb.Append($"-{Variant.Value}");
            return sb.ToString();
        }
    }

    public readonly record struct SemanticVersion(
        int Major,
        Optional<int> Minor = default,
        Optional<int> Patch = default,
        Optional<string> VersionPrefix = default)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (VersionPrefix.HasValue)
                sb.Append(VersionPrefix.Value);
            sb.Append(Major);
            if (Minor.HasValue)
            {
                sb.Append($".{Minor.Value}");
                if (Patch.HasValue)
                    sb.Append($".{Patch.Value}");
            }
            return sb.ToString();
        }

        [JsonIgnore]
        public SemanticVersionPrecison Precison => Minor.HasValue
            ? (Patch.HasValue ? SemanticVersionPrecison.Patch : SemanticVersionPrecison.Minor)
            : SemanticVersionPrecison.Major;

        public static SemanticVersionSize Compare(SemanticVersion from, SemanticVersion to)
        {
            if (from.Precison != to.Precison)
                return SemanticVersionSize.PrecisionMismatch;

            if (to.Major < from.Major)
                return SemanticVersionSize.Downgrade;
            if (to.Major > from.Major)
                return SemanticVersionSize.Major;
            if (!to.Minor.HasValue)
                return SemanticVersionSize.Equal;

            if (to.Minor.Value < from.Minor.Value)
                return SemanticVersionSize.Downgrade;
            if (to.Minor.Value > from.Minor.Value)
                return SemanticVersionSize.Minor;
            if (!to.Patch.HasValue)
                return SemanticVersionSize.Equal;

            if (to.Patch.Value < from.Patch.Value)
                return SemanticVersionSize.Downgrade;
            if (to.Patch.Value > from.Patch.Value)
                return SemanticVersionSize.Patch;

            return SemanticVersionSize.Equal;
        }
    }

    public enum SemanticVersionSize
    {
        Equal,
        Patch,
        Minor,
        Major,
        Downgrade,
        PrecisionMismatch
    }


    public enum SemanticVersionPrecison
    {
        Patch,
        Minor,
        Major
    }
}
