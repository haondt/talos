using Newtonsoft.Json;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Yaml.Models
{
    public record YamlUpdateLocationState : ISubatomicUpdateLocationState
    {
        public required TalosSettings Configuration { get; init; }
        public required YamlUpdateLocationSnapshot Snapshot { get; init; }

        [JsonIgnore]
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot => Snapshot;

        [JsonIgnore]
        IUpdateLocationSnapshot IUpdateLocationState.Snapshot => Snapshot;
    }
}
