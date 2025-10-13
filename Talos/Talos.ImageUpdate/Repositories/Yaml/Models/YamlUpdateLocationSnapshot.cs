using Haondt.Core.Models;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Yaml.Models
{
    public record YamlUpdateLocationSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ParsedImage CurrentImage { get; init; }
        public required string RawCurrentImageString { get; init; }
        public required Optional<string> AnchorName { get; init; }


        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not YamlUpdateLocationSnapshot other)
                return false;

            return this == other;
        }
    }
}