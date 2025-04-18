﻿using Haondt.Core.Models;
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
    }
}
