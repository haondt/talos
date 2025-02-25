namespace Talos.Renovate.Models
{
    public class SkopeoInspectResponse
    {
        public required string Name { get; set; }
        public required string Digest { get; set; }
        public HashSet<string> RepoTags { get; set; } = [];
        public required DateTime Created { get; set; }
        public required string DockerVersion { get; set; }
        public Dictionary<string, string> Labels { get; set; } = [];
        public required string Architecture { get; set; }
        public required string Os { get; set; }
        public List<string> Layers { get; set; } = [];
        public List<string> Env { get; set; } = [];
    }
}
