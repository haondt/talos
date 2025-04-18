using Haondt.Core.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.ImageUpdating.Models
{
    public class ImageUpdateTrace
    {
        public required string Resolution { get; set; }
        public Optional<ImageUpdateData> Cached { get; set; }
        public Optional<IScheduledPush> Push { get; set; }
        public required UpdateIdentity Identity { get; set; }
    }
}
