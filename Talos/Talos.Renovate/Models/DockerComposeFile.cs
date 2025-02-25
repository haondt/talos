namespace Talos.Renovate.Models
{
    public class DockerComposeFile
    {
        public Dictionary<string, Service>? Services { get; set; }

    }

    public class Service
    {
        public string? Image { get; set; }

        public TalosSettings? XTalos { get; set; }


    }

    public class TalosSettings
    {
        public bool Skip { get; set; } = false;
        public BumpSize Bump { get; set; } = BumpSize.Digest;


    }



}
