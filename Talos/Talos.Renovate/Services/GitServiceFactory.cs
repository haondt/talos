using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class GitServiceFactory(IServiceProvider serviceProvider) : IGitServiceFactory
    {
        private Task<IGitService> _gitServiceTask = GitService.CreateAsync(serviceProvider)
            .ContinueWith(g => (IGitService)g.Result);

        public Task<IGitService> CreateAsync()
        {
            return _gitServiceTask;
        }
    }
}
