using Haondt.Core.Models;
using System.Text;

namespace Talos.ImageUpdate.ImageParsing.Models
{
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

        public string ToShortString()
        {
            var sb = new StringBuilder();
            sb.Append(Tag.ToString());
            if (Digest.HasValue)
                if (Digest.Value.StartsWith("sha256:"))
                    sb.Append(string.Concat("@", Digest.Value.AsSpan("sha256:".Length, 8)));
                else
                    sb.Append(string.Concat("@", Digest.Value.AsSpan(0, 8)));
            return sb.ToString();
        }
    }
}
