using Talos.Renovate.Models;

namespace Talos.Domain.Models
{
    public class DiscordImageUpdateInteractionData
    {
        public required ImageUpdate ImageUpdate { get; set; }
        public required string PushButtonId { get; set; }
        public required string DeferButtonId { get; set; }
        public required string IgnoreButtonId { get; set; }
        public required ImageUpdateIdentity UpdateIdentity { get; set; }
    }
}
