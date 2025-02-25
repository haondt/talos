using Newtonsoft.Json;

namespace Talos.Renovate.Models
{
    public class ImageUpdateData
    {
        [JsonRequired]
        public required string Image { get; set; }

        public InteractionData? Interaction { get; set; }
    }

    public class InteractionData
    {
        [JsonRequired]
        public required string PendingImage { get; set; }
        public string? InteractionId { get; set; }
    }
}
