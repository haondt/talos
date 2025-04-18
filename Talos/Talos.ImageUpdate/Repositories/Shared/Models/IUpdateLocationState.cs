using Talos.ImageUpdate.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public interface IUpdateLocationState
    {
        public TalosSettings Configuration { get; }
        public IUpdateLocationSnapshot Snapshot { get; }

    }



}
