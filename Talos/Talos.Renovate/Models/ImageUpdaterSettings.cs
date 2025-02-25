namespace Talos.Renovate.Models
{
    public class ImageUpdaterSettings
    {
        public int RedisDatabase { get; set; } = 1;
        public string DefaultRelease { get; set; } = "latest";
    }
}