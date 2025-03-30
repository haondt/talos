using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdateDataRepository
    {
        Task<bool> SetImageUpdateDataAsync(UpdateIdentity id, ImageUpdateData data);
        Task<Result<ImageUpdateData>> TryGetImageUpdateDataAsync(UpdateIdentity id);
        Task<bool> ClearImageUpdateDataCacheAsync(UpdateIdentity id);
    }
}
