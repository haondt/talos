namespace Talos.Renovate.Models
{
    public class ImageUpdaterSettings
    {
        public int RedisDatabase { get; set; } = 0;
        public string DefaultRelease { get; set; } = "latest";
    }
}