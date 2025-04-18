using Haondt.Core.Models;
using Newtonsoft.Json;
using System.Text;

namespace Talos.ImageUpdate.ImageParsing.Models
{
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
            ? Patch.HasValue ? SemanticVersionPrecison.Patch : SemanticVersionPrecison.Minor
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
}
