namespace Talos.Renovate.Models
{
    public class SkopeoSettings
    {
        public int RedisDatabase { get; set; } = 0;
        public int CacheDurationHours { get; set; } = 12;
        public string SkopeoCommand { get; set; } = "skopeo";
        public List<string> SkopeoArguments { get; set; } = [];
    }
}
