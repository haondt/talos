using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IGitService
    {
        Task<bool> CheckIfHasUpstreamAsync(string repositoryDirectory, string? branchName = null);
        Task<TemporaryDirectory> CloneAsync(HostConfiguration host, RepositoryConfiguration repository);
        Task<string> Commit(string repositoryDirectory,
            string title,
            string? description = null,
            bool all = false);
        Task CreateAndCheckoutBranch(string repoDirectory, string branchName);
        Task<string> GetNameOfCurrentBranchAsync(string repoDirectory);
        Task PullAsync(string repositoryDirectory, bool rebase = false);
        Task PushAsync(HostConfiguration host, RepositoryConfiguration repository, string repositoryDirectory, string? branchName = null, bool force = false, string? setUpstream = null);
    }
}