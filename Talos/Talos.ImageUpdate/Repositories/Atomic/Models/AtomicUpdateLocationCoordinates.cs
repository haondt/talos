using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public record AtomicUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public List<IUpdateLocationCoordinates> Children { get; set; } = [];

        public UpdateIdentity GetIdentity(string repository, Optional<string> branch)
        {
            return UpdateIdentity.Atomic(repository, branch, Children.Select(q => q.GetIdentity(repository, branch)));
        }

        public override string ToString()
        {
            return $"Atomic:{string.Join(';', Children.Select(q => q.ToString()))}";
        }
    }
}
