using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public record ImageUpdateOperation
    {
        public required ParsedImage NewImage { get; init; }
        public required AbsoluteDateTime NewImageCreatedOn { get; init; }
        public required BumpSize BumpSize { get; init; }
    }

}
