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
            sb.Append(':' + TagAndDigest.Value.Tag.ToString());
            if (TagAndDigest.Value.Digest.HasValue)
                if (TagAndDigest.Value.Digest.Value.StartsWith("sha256:"))
                    sb.Append(string.Concat("@", TagAndDigest.Value.Digest.Value.AsSpan("sha256:".Length, 8)));
                else
                    sb.Append(string.Concat("@", TagAndDigest.Value.Digest.Value.AsSpan(0, 8)));
            return sb.ToString();
        }
    }
}
