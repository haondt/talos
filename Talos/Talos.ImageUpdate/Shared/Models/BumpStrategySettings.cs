namespace Talos.ImageUpdate.Shared.Models
{
    public class BumpStrategySettings
    {
        public BumpStrategy Digest { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Patch { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Minor { get; set; } = BumpStrategy.Notify;
        public BumpStrategy Major { get; set; } = BumpStrategy.Notify;
    }
}
