using Haondt.Core.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Models
{
    public interface IPushToFileWriter
    {
        DetailedResult<IUpdateLocationSnapshot, string> Write(string repositoryDirectory);
    }



}
