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

        public DockerHostSSHConfig? SSHConfig { get; set; }
    }

    public class DockerHostSSHConfig
    {
        public required string User { get; set; }
        public required string Host { get; set; }
        public string? Password { get; set; }
        public string? IdentityFile { get; set; }
    }
}
