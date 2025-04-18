using Haondt.Core.Models;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public record ImageUpdateOperation
    {
        public required ParsedImage NewImage { get; init; }
        public required AbsoluteDateTime NewImageCreatedOn { get; init; }
        public required BumpSize BumpSize { get; init; }
    }

}
