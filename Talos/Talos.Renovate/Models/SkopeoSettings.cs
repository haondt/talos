using Microsoft.Extensions.Options;

namespace Talos.Renovate.Models
{
    public class SkopeoSettings
    {
        public int RedisDatabase { get; set; } = 0;
        public int CacheDurationHours { get; set; } = 12;
        public int CacheDurationVarianceHours { get; set; } = 6;
        public string SkopeoCommand { get; set; } = "skopeo";
        public List<string> SkopeoArguments { get; set; } = [];
        public static OptionsBuilder<SkopeoSettings> Validate(OptionsBuilder<SkopeoSettings> builder)
        {
            builder.Validate(o => o.CacheDurationVarianceHours < o.CacheDurationHours, "Cache duration variance must be less than cache duration.");
            return builder;
        }
    }
}
