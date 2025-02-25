
using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdaterService
    {
        Task RunAsync(CancellationToken? cancellationToken = null);
        Task<Optional<ImageUpdate>> SelectUpdateTarget(string image, BumpSize maxBumpSize);
    }
}