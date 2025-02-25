using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public class ImageUpdateTrace
    {
        public required string Resolution { get; set; }
        public string? DesiredImage { get; set; }
        public AbsoluteDateTime? DesiredImageCreatedOn { get; set; }
        public required string CurrentImage { get; set; }
        public string? CachedImage { get; set; }
        public AbsoluteDateTime? CachedImageCreatedOn { get; set; }
    }
}
