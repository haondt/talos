using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public interface ISubatomicUpdateLocationState : IUpdateLocationState
    {
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot { get; }

    }



}
