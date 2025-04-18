using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
    public record DockerfileUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public required string RelativeFilePath { get; init; }
        public required int Line { get; init; }

        public UpdateIdentity GetIdentity(string repository, Optional<string> branch)
        {
            return UpdateIdentity.Dockerfile(repository, branch, RelativeFilePath, Line);
        }
        public override string ToString()
        {
            return $"Dockerfile:{RelativeFilePath}:{Line}";
        }

    }
}
