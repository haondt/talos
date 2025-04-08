using Haondt.Core.Models;
using System.Text.RegularExpressions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageParser
    {
        ParsedImage Parse(string image, bool insertDefaultDomain);
        Optional<ParsedImage> TryParse(string image, bool insertDefaultDomain);
        Optional<ParsedTag> TryParseTag(string tag);
        Optional<ParsedTag> TryParseTag(Match match);
        Optional<ParsedTagAndDigest> TryParseTagAndDigest(string tagAndDigest);
    }
}