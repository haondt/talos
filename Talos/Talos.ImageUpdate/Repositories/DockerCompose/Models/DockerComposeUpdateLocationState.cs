using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
    public record DockerComposeUpdateLocationState : ISubatomicUpdateLocationState
    {
        public required TalosSettings Configuration { get; init; }
        public required DockerComposeUpdateLocationSnapshot Snapshot { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot => Snapshot;

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }


}
