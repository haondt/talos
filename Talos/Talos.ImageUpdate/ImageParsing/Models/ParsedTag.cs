using Haondt.Core.Models;
using System.Text;

namespace Talos.ImageUpdate.ImageParsing.Models
{
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
}
