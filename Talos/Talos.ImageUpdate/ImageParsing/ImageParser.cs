using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Talos.ImageUpdate.ImageParsing.Models;

namespace Talos.ImageUpdate.ImageParsing
{
    public class ImageParser : IImageParser
    {
        private const string DEFAULT_DOMAIN_NAMESPACE = "library";
        private const string DEFAULT_DOMAIN = "docker.io";
        private readonly Regex _imageRegex;
        private readonly Regex _tagAndDigestRegex;
        private readonly Regex _tagRegex;

        public ImageParser(IOptions<ImageParserSettings> options, ILogger<ImageParser> logger)
        {
            var tagPattern = $@"(?<versionprefix>v)?(?:(?:(?<major>\d{{1,6}})(?:\.(?<minor>\d{{1,6}})(?:\.(?<patch>\d{{1,6}}))?)?)|(?<release>{string.Join('|', options.Value.ValidReleases.Select(Regex.Escape))}))(?:-(?<variant>\w+))?";
            var tagAndDigestPattern = $@"(?<tag>{tagPattern})(?:@(?<digest>sha\d+:[a-f0-9]+))?";
            var imagePattern = $@"(?<untagged>(?:(?<domain>[\w.\-_]+\.[\w.\-_]+(?::\d+)?)/)?(?:(?<namespace>(?:[\w.\-_]+)(?:/[\w.\-_]+)*)/)?(?<name>[a-z0-9.\-_]+))(?::(?<taganddigest>{tagAndDigestPattern}))?";
            _imageRegex = new($"^{imagePattern}$");
            _tagAndDigestRegex = new($"^{tagAndDigestPattern}$");
            _tagRegex = new($"^{tagPattern}$");
        }

        public Optional<ParsedImage> TryParse(string image, bool insertDefaultDomain)
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
                version = new(new SemanticVersion(
                    VersionPrefix: TryExtractNonEmptyGroup(match, "versionprefix"),
                    Major: int.Parse(majorString.Value),
                    Minor: TryExtractNonEmptyGroup(match, "minor").As(int.Parse),
                    Patch: TryExtractNonEmptyGroup(match, "patch").As(int.Parse)));
            else if (TryExtractNonEmptyGroup(match, "release").TryGetValue(out var release))
                version = new(release);
            else
                return new();

            var variant = TryExtractNonEmptyGroup(match, "variant");
            return new(new(version, variant));
        }




        public ParsedImage Parse(string image, bool insertDefaultDomain)
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
}
