using Haondt.Core.Models;
using Newtonsoft.Json;

namespace Talos.Renovate.Models
{
    public class ImageUpdateData
    {
        [JsonRequired]
        public required IUpdateLocationSnapshot LastKnownSnapshot { get; set; }
        public Optional<InteractionData> Interaction { get; set; }
        public Optional<IScheduledPush> LastNotified { get; set; }
    }

    public class InteractionData
    {
        [JsonRequired]
        public required IScheduledPush PendingPush { get; set; }
        public Optional<string> InteractionId { get; set; }

    }
}
