using Haondt.Core.Models;
using Talos.ImageUpdate.ImageUpdating.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.ImageUpdating.Services
{
    public interface IImageUpdateDataRepository
    {
        Task<bool> SetImageUpdateDataAsync(UpdateIdentity id, ImageUpdateData data);
        Task<Result<ImageUpdateData>> TryGetImageUpdateDataAsync(UpdateIdentity id);
        Task<bool> ClearImageUpdateDataCacheAsync(UpdateIdentity id);
    }
}
