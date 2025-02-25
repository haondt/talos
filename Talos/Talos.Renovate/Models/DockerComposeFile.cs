using YamlDotNet.Serialization;

namespace Talos.Renovate.Models
{
    public class DockerComposeFile
    {
        public Dictionary<string, Service>? Services { get; set; }
    }

    public class Service
    {
        public string? Image { get; set; }

        [YamlMember(Alias = "x-talos", ApplyNamingConventions = false)]
        public TalosSettings? XTalos { get; set; }
    }

    public class TalosSettings
    {
        public bool Skip { get; set; } = false;
        public BumpSize Bump { get; set; } = BumpSize.Digest;
        public BumpStrategySettings Strategy { get; set; } = new();
    }

    public class BumpStrategySettings
    {
        public BumpStrategy Digest { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Patch { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Minor { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Major { get; set; } = BumpStrategy.Notify;
    }

    public enum BumpStrategy
    {
        Notify,
        Prompt,
        Skip,
        Push,
    }
}
