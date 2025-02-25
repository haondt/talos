
using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdaterService
    {
        Task RunAsync();
        Task<Optional<(string DesiredImage, BumpSize BumpSize)>> SelectUpdateTarget(string image, BumpSize maxBumpSize);
    }
}