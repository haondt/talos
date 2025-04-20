using Haondt.Core.Models;
using System.Text;

namespace Talos.ImageUpdate.ImageParsing.Models
{
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
            sb.Append(':' + TagAndDigest.Value.ToShortString());
            return sb.ToString();
        }

        public static string DiffString(ParsedImage source, Optional<ParsedTagAndDigest> destination)
        {
            var sb = new StringBuilder(source.Name);
            sb.Append(": ");
            if (source.TagAndDigest.HasValue)
                sb.Append(source.TagAndDigest.Value.ToShortString());
            else
                sb.Append("(untagged)");
            sb.Append(" → ");
            if (destination.HasValue)
                sb.Append(destination.Value.ToShortString());
            else
                sb.Append("(untagged)");
            return sb.ToString();
        }
    }
}
