using Haondt.Core.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public interface IUpdateLocation
    {
        IUpdateLocationCoordinates Coordinates { get; }
        IUpdateLocationState State { get; }

        Task<Optional<IScheduledPush>> CreateScheduledPushAsync(IImageUpdaterService imageUpdaterService);
    }
}
