using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public readonly record struct ImageUpdate(
        ParsedImage PreviousImage,
        ParsedImage NewImage,
        AbsoluteDateTime NewImageCreatedOn,
        BumpSize BumpSize);
}
