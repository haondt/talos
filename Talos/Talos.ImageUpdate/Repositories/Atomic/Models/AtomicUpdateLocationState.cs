using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public record class AtomicUpdateLocationState : IUpdateLocationState
    {
        public required AtomicUpdateLocationSnapshot Snapshot { get; init; }
        public required TalosSettings Configuration { get; init; }

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }
}
