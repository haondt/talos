using Newtonsoft.Json;

namespace Talos.Renovate.Models
{
    public class GitLabMergeRequestDto
    {
        [JsonRequired]
        [JsonProperty("iid")]
        public required string Iid { get; set; }

        [JsonProperty("web_url")]
        public string? WebUrl { get; set; }
    }
}
