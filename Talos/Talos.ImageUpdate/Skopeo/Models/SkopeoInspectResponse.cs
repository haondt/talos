using Newtonsoft.Json;

namespace Talos.ImageUpdate.Skopeo.Models
{
    public class SkopeoInspectResponse
    {
        [JsonRequired]
        public required string Name { get; set; }
        [JsonRequired]
        public required string Digest { get; set; }
        public HashSet<string> RepoTags { get; set; } = [];

        [JsonRequired]
        public required DateTime Created { get; set; }
        [JsonRequired]
        public required string DockerVersion { get; set; }
        public Dictionary<string, string> Labels { get; set; } = [];
        [JsonRequired]
        public required string Architecture { get; set; }
        [JsonRequired]
        public required string Os { get; set; }
        public List<string> Layers { get; set; } = [];
        public List<string> Env { get; set; } = [];
    }
}
