using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
    public record DockerfileUpdateLocationSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ParsedImage CurrentImage { get; init; }
        public required byte[] LineHash { get; init; }

        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not DockerfileUpdateLocationSnapshot other)
                return false;
            return this == other;
        }
    }
}
