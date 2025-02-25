using Haondt.Core.Models;
using Newtonsoft.Json;

namespace Talos.Renovate.Models
{
    public class ImageUpdateData
    {
        [JsonRequired]
        public required string Image { get; set; }

        public InteractionData? Interaction { get; set; }

        public LastNotifiedData? LastNotified { get; set; }
    }

    public class InteractionData
    {
        [JsonRequired]
        public required string PendingImage { get; set; }
        [JsonRequired]
        public required AbsoluteDateTime PendingImageCreatedOn { get; set; }
        public string? InteractionId { get; set; }
    }

    public class LastNotifiedData
    {
        [JsonRequired]
        public required string Image { get; set; }
        [JsonRequired]
        public required AbsoluteDateTime CreatedOn { get; set; }
    }
}
