using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Dockerfile.Models
{
    public record DockerfileUpdateLocationState : ISubatomicUpdateLocationState
    {
        public required TalosSettings Configuration { get; init; }
        public required DockerfileUpdateLocationSnapshot Snapshot { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot => Snapshot;

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }
}
