using Haondt.Core.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public interface IUpdateLocationCoordinates
    {
        public UpdateIdentity GetIdentity(string repository, Optional<string> branch);
    }



}
