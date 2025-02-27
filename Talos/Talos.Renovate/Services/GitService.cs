using Microsoft.Extensions.DependencyInjection;
using Talos.Integration.Command.Abstractions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Extensions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class GitService : IGitService
    {
        private readonly ICommandFactory commandFactory;

        private const string GIT_USER = "Talos";
        private const string GIT_EMAIL = "talos@example.com";
        private const string GIT_BINARY = "git";

        public GitService(ICommandFactory _commandFactory)
        {
            commandFactory = _commandFactory;
        }

        public static async Task<GitService> CreateAsync(IServiceProvider serviceProvider)
        {
            var git = ActivatorUtilities.CreateInstance<GitService>(serviceProvider);
            await git.ConfigureGitEnvironmentAsync();
            return git;
        }

        private async Task ConfigureGitEnvironmentAsync()
        {
            await commandFactory.Create(GIT_BINARY)
                .WithArguments(ab => ab
                    .Add("config")
                    .Add("--global")
                    .Add("user.email")
                    .Add(GIT_EMAIL))
                .ExecuteAsync();

            await commandFactory.Create(GIT_BINARY)
                .WithArguments(ab => ab
                    .Add("config")
                    .Add("--global")
                    .Add("user.name")
                    .Add(GIT_USER))
                .ExecuteAsync();
        }

        public Task CreateAndCheckoutBranch(string repositoryDirectory, string branchName)
        {
            return commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("checkout")
                    .Add("-b")
                    .Add(branchName))
                .ExecuteAsync();
        }

        public async Task<string> GetNameOfCurrentBranchAsync(string repositoryDirectory)
        {
            var result = await commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("rev-parse")
                    .Add("--abbrev-ref")
                    .Add("HEAD"))
                .ExecuteAndCaptureStdoutAsync();
            return result.Trim();
        }

        private (string Url, List<string> SensitiveStrings) GetAuthenticatedGitUrl(HostConfiguration host, RepositoryConfiguration repository)
        {
            var uri = new Uri(repository.Url);
            if (!string.IsNullOrEmpty(host.Token))
                return ($"{uri.Scheme}://{GIT_USER}:{host.Token}@{uri.Host}:{uri.Port}/{uri.AbsolutePath.TrimStart('/')}".TrimEnd('/'), [host.Token]);
            return ($"{uri.Scheme}://{uri.Host}:{uri.Port}/{uri.AbsolutePath.TrimStart('/')}".TrimEnd('/'), []);
        }

        public async Task<TemporaryDirectory> CloneAsync(HostConfiguration host, RepositoryConfiguration repository)
        {
            var repoDir = new TemporaryDirectory();
            try
            {
                var (url, sensitiveStrings) = GetAuthenticatedGitUrl(host, repository);
                var command = commandFactory.Create(GIT_BINARY)
                        .WithArguments(ab => ab
                            .Add("clone")
                            .AddIf(!string.IsNullOrEmpty(repository.Branch), "-b")
                            .AddIf(!string.IsNullOrEmpty(repository.Branch), repository.Branch!)
                            .Add(url)
                            .Add(repoDir.Path));
                foreach (var sensitiveString in sensitiveStrings)
                    command = command.WithSensitiveDataMasked(sensitiveString);
                await command.ExecuteAsync();
                return repoDir;
            }
            catch
            {
                repoDir.Dispose();
                throw;
            }
        }

        public Task CommitAllWithMessageAsync(string repositoryDirectory, string message)
        {
            return commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("commit")
                    .Add("-am")
                    .Add(message))
                .ExecuteAndCaptureStdoutAsync();
        }

        public Task PushAsync(
            HostConfiguration host,
            RepositoryConfiguration repository,
            string repositoryDirectory,
            string? branchName = null,
            bool force = false,
            string? setUpstream = null)
        {
            var (url, sensitiveStrings) = GetAuthenticatedGitUrl(host, repository);
            var command = commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("push")
                    .AddIf(force, "-f")
                    .AddIf(!string.IsNullOrEmpty(setUpstream), "-u")
                    .AddIf(!string.IsNullOrEmpty(setUpstream), setUpstream!)
                    .Add(url)
                    .Add(branchName ?? "HEAD"));

            foreach (var sensitiveString in sensitiveStrings)
                command = command.WithSensitiveDataMasked(sensitiveString);

            return command.ExecuteAsync();
        }

        public async Task<bool> CheckIfHasUpstreamAsync(string repositoryDirectory, string? branchName = null)
        {
            var result = await commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("rev-parse")
                    .Add("--abbrev-ref")
                    .Add($"'{branchName ?? "HEAD"}@{{upstream}}'"))
                .WithAllowedNonZeroExitCode()
                .ExecuteAsync();
            return result.ExitCode != 0;
        }
        public Task PullAsync(string repositoryDirectory, bool rebase = false)
        {
            return commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("pull")
                    .AddIf(rebase, "--rebase"))
                .ExecuteAndCaptureStdoutAsync();
        }
    }
}
