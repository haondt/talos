using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public partial class ImageUpdaterService
    {
        private const string UPDATES_BRANCH_NAME_PREFIX = "talos/updates";
        private const string GIT_NAME = "Talos";
        private const string GIT_EMAIL = "talos@example.com";

        public static string GetUpdatesBranchName(string targetBranchName) => $"{UPDATES_BRANCH_NAME_PREFIX}-{targetBranchName}";


        private static CredentialsHandler GetCredentialsHandler(HostConfiguration host)
        {
            if (host.Type != HostType.GitLab)
                throw new NotSupportedException($"Unable to handle hosts of type {host.Type}");
            if (string.IsNullOrEmpty(host.Token))
                throw new InvalidOperationException("Token is required for GitLab host");
            return (_url, _user, _cred) =>
                new UsernamePasswordCredentials { Username = host.Username ?? "oauth2", Password = host.Token };
        }

        private static TemporaryDirectory CloneRepository(HostConfiguration host, RepositoryConfiguration repository)
        {
            var cloneOptions = new CloneOptions();
            if (!string.IsNullOrEmpty(repository.Branch))
                cloneOptions.BranchName = repository.Branch;
            cloneOptions.FetchOptions.Depth = 1;

            cloneOptions.FetchOptions.CredentialsProvider = GetCredentialsHandler(host);

            var repoDir = new TemporaryDirectory();
            Repository.Clone(repository.Url, repoDir.Path, cloneOptions);

            return repoDir;
        }

        private static Branch CreateAndCheckoutUpdatesBranch(Repository repo, string targetbranchName)
        {
            var branch = repo.CreateBranch(GetUpdatesBranchName(targetbranchName));
            Commands.Checkout(repo, branch);
            return branch;
        }

        private static Commit CommitAllWithMessage(Repository repo, string message)
        {
            Commands.Stage(repo, "*");
            var signature = new Signature(GIT_NAME, GIT_EMAIL, DateTimeOffset.Now);
            return repo.Commit(message, signature, signature);
        }

        private static void ForcePushUpdatesBranch(Repository repo, HostConfiguration host, string targetBranchName)
        {
            var pushOptions = new PushOptions
            {
                CredentialsProvider = GetCredentialsHandler(host)
            };
            var updatesBranchName = GetUpdatesBranchName(targetBranchName);
            repo.Network.Push(repo.Network.Remotes.Single(), $"+{updatesBranchName}:refs/heads/{updatesBranchName}", pushOptions);
        }
    }
}
