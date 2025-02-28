using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Talos.Renovate.Models
{
    public static class ImageParser
    {
        private const string IMAGE_PATTERN = $@"(?<untagged>(?:(?<domain>[\w.\-_]+\.[\w.\-_]+(?::\d+)?)/)?(?:(?<namespace>(?:[\w.\-_]+)(?:/[\w.\-_]+)*)/)?(?<name>[a-z0-9.\-_]+))(?::(?<taganddigest>{TAG_AND_DIGEST_PATTERN}))?";

        private const string TAG_AND_DIGEST_PATTERN = $@"(?<tag>{TAG_PATTERN})(?:@(?<digest>sha\d+:[a-f0-9]+))?";

        private const string TAG_PATTERN = @"(?<versionprefix>v)?(?:(?:(?<major>\d+)(?:\.(?<minor>\d+)(?:\.(?<patch>\d+))?)?)|(?<release>latest|stable))(?:-(?<variant>\w+))?";

        private const string DEFAULT_DOMAIN_NAMESPACE = "library";
        private const string DEFAULT_DOMAIN = "docker.io";

        public static Optional<ParsedImage> TryParse(string image, bool insertDefaultDomain = false)
        {
            var match = Regex.Match(image, $"^{IMAGE_PATTERN}$");

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

        public static Optional<ParsedTagAndDigest> TryParseTagAndDigest(string tagAndDigest)
        {
            var match = Regex.Match(tagAndDigest, $"^{TAG_AND_DIGEST_PATTERN}$");
            if (!match.Success)
                return new();
            return TryParseTagAndDigest(match);
        }
        private static Optional<ParsedTagAndDigest> TryParseTagAndDigest(Match match)
        {
            var digest = TryExtractNonEmptyGroup(match, "digest");
            var tag = TryParseTag(match);
            if (!tag.HasValue)
                return new();

            return new(new(tag.Value, digest));
        }

        public static Optional<ParsedTag> TryParseTag(string tag)
        {
            var match = Regex.Match(tag, $"^{TAG_PATTERN}$");
            if (!match.Success)
                return new();
            return TryParseTag(match);
        }

        public static Optional<ParsedTag> TryParseTag(Match match)
        {
            Union<SemanticVersion, string> version;
            var majorString = TryExtractNonEmptyGroup(match, "major");
            if (majorString.HasValue)
            {
                version = new(new SemanticVersion(
                    VersionPrefix: TryExtractNonEmptyGroup(match, "versionprefix"),
                    Major: int.Parse(majorString.Value),
                    Minor: TryExtractNonEmptyGroup(match, "minor").As(int.Parse),
                    Patch: TryExtractNonEmptyGroup(match, "patch").As(int.Parse)));
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




        public static ParsedImage Parse(string image, bool insertDefaultDomain = false)
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
