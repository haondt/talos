using Haondt.Core.Models;

namespace Talos.Docker.Models
{
    public class DockerClientOptions
    {
        public DockerVersion DockerVersion { get; set; } = DockerVersion.V2;
        public Optional<string> Host { get; set; }
        public bool ForceRecreateOnUp { get; set; } = false;
    }

    public enum DockerVersion
    {
        V1,
        V2
    }
}
