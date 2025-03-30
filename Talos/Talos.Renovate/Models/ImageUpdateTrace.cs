using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public class ImageUpdateTrace
    {
        public required string Resolution { get; set; }
        public Optional<ImageUpdateData> Cached { get; set; }
        public Optional<IScheduledPush> Push { get; set; }
        public required UpdateIdentity Identity { get; set; }
    }
}
