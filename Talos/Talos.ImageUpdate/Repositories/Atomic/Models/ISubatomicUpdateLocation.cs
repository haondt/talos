using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public interface ISubatomicUpdateLocation : IUpdateLocation
    {
        ISubatomicUpdateLocationState SubatomicState { get; }
        ISubatomicPushToFileWriter CreateWriter(ImageUpdateOperation updateOperation);

    }



}
