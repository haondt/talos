using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.GitHosts.Shared.Models;

namespace Talos.ImageUpdate.GitHosts.Shared.Services
{
    public class GitHostServiceProvider(IEnumerable<IGitHostService> gitHostServices) : IGitHostServiceProvider
    {
        private Dictionary<HostType, IGitHostService> _gitHostServices = gitHostServices.GroupBy(q => q.Type)
            .ToDictionary(q => q.Key, q => q.First());

        public IGitHostService GetGitHost(HostConfiguration host)
        {
            return _gitHostServices[host.Type];
        }
    }
}
