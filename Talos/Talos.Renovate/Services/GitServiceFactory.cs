using System.Threading.Tasks;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class GitServiceFactory(IServiceProvider serviceProvider) : IGitServiceFactory
    {
        private Task<IGitService>? _gitServiceTask;

        public async Task<IGitService> CreateAsync()
        {
            if (_gitServiceTask == null)
            {
                _gitServiceTask = GitService.Create(serviceProvider);
            }
            return await _gitServiceTask;
        }
    }
}
