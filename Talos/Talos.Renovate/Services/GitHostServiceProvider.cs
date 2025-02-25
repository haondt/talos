using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
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
