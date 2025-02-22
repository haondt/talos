namespace Talos.Docker.Models
{
    public class DockerSettings
    {
        public Dictionary<string, DockerHostSettings> Hosts { get; set; } = [];
    }

    public class DockerHostSettings
    {
        public DockerVersion DockerVersion { get; set; } = DockerVersion.V2;
        public string Host { get; set; } = DockerConstants.LOCALHOST;
        public bool ForceRecreateOnUp { get; set; } = false;
    }
}
