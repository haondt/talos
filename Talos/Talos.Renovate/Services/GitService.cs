using Microsoft.Extensions.Options;
using Talos.Integration.Command.Abstractions;
using Talos.Integration.Command.Services;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Extensions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class GitService : IGitService
    {
        private readonly ICommandFactory _commandFactory;
        private readonly GitSettings _gitSettings;
        private const string DEFAULT_GIT_USER_NAME = "Talos";
        private const string DEFAULT_GIT_EMAIL = "talos@example.com";
        private const string GIT_BINARY = "git";

        public GitService(ICommandFactory commandFactory, IOptions<GitSettings> gitSettings)
        {
            _commandFactory = commandFactory;
            _gitSettings = gitSettings.Value;
        }

        private static string GetUserName(HostConfiguration host) => host.Name ?? DEFAULT_GIT_USER_NAME;
        private static string GetEmail(HostConfiguration host) => host.Email ?? DEFAULT_GIT_EMAIL;

        private async Task ConfigureGitEnvironmentAsync(HostConfiguration host, string repositoryDirectory)
        {
            var commands = new List<CommandBuilder>
            {
                _commandFactory.Create(GIT_BINARY)
                    .WithArguments(ab => ab
                        .Add("config")
                        .Add("user.email")
                        .Add(GetEmail(host))),
                _commandFactory.Create(GIT_BINARY)
                    .WithArguments(ab => ab
                        .Add("config")
                        .Add("user.name")
                        .Add(GetUserName(host)))
            };

            if (!string.IsNullOrEmpty(repositoryDirectory))
                foreach (var command in commands)
                    await command.WithWorkingDirectory(repositoryDirectory).ExecuteAsync();
            else
                foreach (var command in commands)
                    await command.ExecuteAsync();
        }

        public Task CreateAndCheckoutBranch(string repositoryDirectory, string branchName)
        {
            return _commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("checkout")
                    .Add("-b")
                    .Add(branchName))
                .ExecuteAsync();
        }

        public async Task<string> GetNameOfCurrentBranchAsync(string repositoryDirectory)
        {
            var result = await _commandFactory.Create(GIT_BINARY)
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
                return ($"{uri.Scheme}://{GetUserName(host)}:{host.Token}@{uri.Host}:{uri.Port}/{uri.AbsolutePath.TrimStart('/')}".TrimEnd('/'), [host.Token]);
            return ($"{uri.Scheme}://{uri.Host}:{uri.Port}/{uri.AbsolutePath.TrimStart('/')}".TrimEnd('/'), []);
        }

        public async Task<TemporaryDirectory> CloneAsync(HostConfiguration host, RepositoryConfiguration repository)
        {
            var repoDir = new TemporaryDirectory();
            try
            {
                var (url, sensitiveStrings) = GetAuthenticatedGitUrl(host, repository);
                var command = _commandFactory.Create(GIT_BINARY)
                        .WithArguments(ab => ab
                            .Add("clone")
                            .AddIf(!string.IsNullOrEmpty(repository.Branch), ["-b", repository.Branch!])
                            .Add(url)
                            .Add(repoDir.Path));
                foreach (var sensitiveString in sensitiveStrings)
                    command = command.WithSensitiveDataMasked(sensitiveString);
                await command.ExecuteAsync();

                await ConfigureGitEnvironmentAsync(host, repoDir.Path);

                return repoDir;
            }
            catch
            {
                repoDir.Dispose();
                throw;
            }
        }

        public async Task<string> Commit(string repositoryDirectory,
            string title,
            string? description = null,
            bool all = false)
        {
            await _commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("commit")
                    .AddIf(all, "-a")
                    .AddRange(["-m", title])
                    .AddIf(!string.IsNullOrEmpty(description), ["-m", description!]))
                .ExecuteAndCaptureStdoutAsync();

            var sha = await _commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("rev-parse")
                    .Add("HEAD"))
                .ExecuteAndCaptureStdoutAsync();
            return sha.Trim();
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
            var command = _commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("push")
                    .AddIf(force, "-f")
                    .AddIf(!string.IsNullOrEmpty(setUpstream), ["-u", setUpstream!])
                    .Add(url)
                    .Add(branchName ?? "HEAD"));

            foreach (var sensitiveString in sensitiveStrings)
                command = command.WithSensitiveDataMasked(sensitiveString);

            return command.ExecuteAsync();
        }

        public async Task<bool> CheckIfHasUpstreamAsync(string repositoryDirectory, string? branchName = null)
        {
            var result = await _commandFactory.Create(GIT_BINARY)
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
            return _commandFactory.Create(GIT_BINARY)
                .WithWorkingDirectory(repositoryDirectory)
                .WithArguments(ab => ab
                    .Add("pull")
                    .AddIf(rebase, "--rebase"))
                .ExecuteAndCaptureStdoutAsync();
        }
    }
}
