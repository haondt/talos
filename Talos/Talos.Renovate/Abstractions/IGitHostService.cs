using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IGitHostService
    {
        HostType Type { get; }
        Task<bool> HasOpenMergeRequestsForBranch(RepositoryConfiguration repository, string sourceBranchName, CancellationToken? cancellationToken = null);
        Task<string> CreateMergeRequestForBranch(RepositoryConfiguration repository, HostConfiguration host, string sourceBranch, string targetBranch, CancellationToken? cancellationToken = null);
    }
}
