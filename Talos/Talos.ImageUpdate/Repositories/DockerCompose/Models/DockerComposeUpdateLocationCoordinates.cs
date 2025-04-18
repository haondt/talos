using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
    public record DockerComposeUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public required string RelativeFilePath { get; init; }
        public required string ServiceKey { get; init; }

        public UpdateIdentity GetIdentity(string repository, Optional<string> branch)
        {
            return UpdateIdentity.DockerCompose(repository, branch, RelativeFilePath, ServiceKey);
        }

        public override string ToString()
        {
            return $"DockerCompose:{RelativeFilePath}:{ServiceKey}";
        }
    }


}
