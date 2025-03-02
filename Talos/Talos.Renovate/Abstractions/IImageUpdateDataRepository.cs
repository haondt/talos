using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdateDataRepository
    {
        Task<bool> SetImageUpdateDataAsync(ImageUpdateIdentity id, ImageUpdateData data);
        Task<Optional<ImageUpdateData>> TryGetImageUpdateDataAsync(ImageUpdateIdentity id);
        Task<bool> ClearImageUpdateDataCacheAsync(ImageUpdateIdentity id);
    }
}
