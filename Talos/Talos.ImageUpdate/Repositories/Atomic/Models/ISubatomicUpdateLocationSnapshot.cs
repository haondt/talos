using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Atomic.Models
{
    public interface ISubatomicUpdateLocationSnapshot : IUpdateLocationSnapshot
    {
        public ParsedImage CurrentImage { get; }
    }



}
