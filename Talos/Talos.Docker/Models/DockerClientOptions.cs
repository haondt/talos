namespace Talos.Docker.Models
{
    public class DockerClientOptions
    {
        public DockerVersion DockerVersion { get; set; } = DockerVersion.V2;
        public required DockerHostOptions HostOptions { get; set; }
        public bool ForceRecreateOnUp { get; set; } = false;
    }

    public enum DockerVersion
    {
        V1,
        V2
    }

    public abstract class DockerHostOptions
    {
    }

    public class LocalDockerHostOptions : DockerHostOptions { }

    public abstract class SSHDockerHostOptions : DockerHostOptions
    {
        public required string User { get; set; }
        public required string Host { get; set; }
    }

    public class SSHIdentityFileDockerHostOptions : SSHDockerHostOptions
    {
        public required string IdentityFile { get; set; }
    }
}

