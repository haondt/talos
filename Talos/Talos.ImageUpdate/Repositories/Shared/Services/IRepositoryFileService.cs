using Haondt.Core.Models;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;

namespace Talos.ImageUpdate.Repositories.Shared.Services
{
    public interface IRepositoryFileService
    {
        List<DetailedResult<IUpdateLocation, string>> ExtractLocations(RepositoryConfiguration repositoryConfiguration, string repositoryDirectory);
    }
}