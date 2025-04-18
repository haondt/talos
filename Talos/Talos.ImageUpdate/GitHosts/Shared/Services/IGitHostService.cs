using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.GitHosts.Shared.Models;

namespace Talos.ImageUpdate.GitHosts.Shared.Services
{
    public interface IGitHostService
    {
        HostType Type { get; }
        Task<bool> HasOpenMergeRequestsForBranch(RepositoryConfiguration repository, string sourceBranchName, CancellationToken? cancellationToken = null);
        Task<string> CreateMergeRequestForBranch(RepositoryConfiguration repository, HostConfiguration host, string sourceBranch, string targetBranch, CancellationToken? cancellationToken = null);
    }
}
