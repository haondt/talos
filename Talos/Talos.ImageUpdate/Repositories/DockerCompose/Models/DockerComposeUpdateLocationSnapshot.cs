using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
    public record DockerComposeUpdateLocationSnapshot : ISubatomicUpdateLocationSnapshot
    {
        public required ParsedImage CurrentImage { get; init; }
        public required string RawCurrentImageString { get; init; }


        public bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot)
        {
            if (locationSnapshot is not DockerComposeUpdateLocationSnapshot other)
                return false;

            return this == other;
        }
    }


}
