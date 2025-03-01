using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text;
using System.Text.RegularExpressions;
using Talos.Core.Models;
using Talos.Integration.Command.Exceptions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public partial class ImageUpdaterService(
        IOptions<ImageUpdateSettings> updateOptions,
        IOptions<ImageUpdaterSettings> options,
        ILogger<ImageUpdaterService> _logger,
        INotificationService _notificationService,
        ISkopeoService _skopeoService,
        IRedisProvider redisProvider,
        IGitHostServiceProvider gitHostServiceProvider,
        IDockerComposeFileService _dockerComposeFileService,
        IGitService _git,
        IPushQueueMutator _pushQueue) : IImageUpdaterService
    {
        private int _isRunning = 0;

        private const string UPDATES_BRANCH_NAME_PREFIX = "talos/updates";
        private readonly ImageUpdateSettings _updateSettings = updateOptions.Value;
        private readonly IDatabase _redis = redisProvider.GetDatabase(options.Value.RedisDatabase);
        public static string GetUpdatesBranchName(string targetBranchName) => $"{UPDATES_BRANCH_NAME_PREFIX}-{targetBranchName}";


        public async Task RunUpdateAsync(CancellationToken? cancellationToken = null)
        {
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                throw new InvalidOperationException("An image update run is already in progress, please try again later.");

            try
            {

                _logger.LogInformation("Starting image update run.");

                var tasks = _updateSettings.Repositories.Select(async r =>
                {
                    try
                    {
                        using (_logger.BeginScope(new Dictionary<string, object>
                        {
                            ["Host"] = r.Host
                        }))
                        {
                            var host = _updateSettings.Hosts[r.Host];
                            await RunAsync(host, r, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Image update on repository {RepositoryUrl} failed due to exception: {ExceptionMessage}", r.Url, ex.Message);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogInformation("Image update run complete.");
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        public Task<bool> CheckIfCommitBelongsToUs(string commit)
        {
            return _redis.SetContainsAsync(RedisNamespacer.Git.Commits, commit);
        }


        private async Task RunAsync(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, CancellationToken? cancellationToken = null)
        {
            using var repoDir = await _git.CloneAsync(host, repositoryConfiguration);
            var processingTasks = _dockerComposeFileService.ExtractUpdateTargets(repositoryConfiguration, repoDir.Path)
                .Select(q =>
                {
                    try
                    {
                        return ProcessService(q.Id, q.Configuration, q.Image);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Processing update for image {Image} failed due to exception: {ExceptionMessage}", q.Id, ex.Message);
                        return Task.FromResult(new Optional<ScheduledPush>());
                    }
                });

            var scheduledPushes = (await Task.WhenAll(processingTasks))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            if (scheduledPushes.Count == 0)
                return;

            // if any pushes are failed to be scheduled, its fine, we are continually
            // scheduling them anyways so they'll get scheduled again on next run
            List<Exception> exceptions = [];
            foreach (var push in scheduledPushes)
            {
                try
                {
                    await _pushQueue.UpsertAndEnqueuePush(push);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Enqueueing push {Push} failed due to exception: {Exception}", push.Target, ex.Message);
                    exceptions.Add(ex);
                }
            }
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        }

        public (HostConfiguration Host, RepositoryConfiguration Repository) GetRepositoryConfiguration(string remoteUrl)
        {
            var repositoryConfiguration = _updateSettings.Repositories.Single(r => r.NormalizedUrl == remoteUrl);
            var hostConfiguration = updateOptions.Value.Hosts[repositoryConfiguration.Host];
            return (hostConfiguration, repositoryConfiguration);
        }

        public async Task PushUpdates(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, List<ScheduledPush> scheduledPushes, CancellationToken? cancellationToken = null)
        {
            using var repoDir = await _git.CloneAsync(host, repositoryConfiguration);

            // this will clone and checkout the target branch if its set, otherwise it will take the default branch
            var targetBranchName = await _git.GetNameOfCurrentBranchAsync(repoDir.Path);
            if (repositoryConfiguration.CreateMergeRequestsForPushes)
                await _git.CreateAndCheckoutBranch(repoDir.Path, GetUpdatesBranchName(targetBranchName));

            foreach (var push in scheduledPushes)
            {
                var filePath = Path.Combine(repoDir.Path, push.Target.RelativeFilePath);
                var content = File.ReadAllText(filePath);
                var updatedContent = _dockerComposeFileService.SetServiceImage(content, push.Target.ServiceKey, push.Update.NewImage.ToString());
                File.WriteAllText(filePath, updatedContent);
            }

            var commitTitle = "[Talos] Updating images";
            var commitDescriptionSb = new StringBuilder();
            foreach (var grp in scheduledPushes.GroupBy(q => q.Target.RelativeFilePath))
            {
                commitDescriptionSb.AppendLine(grp.Key);
                foreach (var push in grp)
                    commitDescriptionSb.AppendLine($"  {push.Target.ServiceKey}: {push.Update.PreviousImage.ToShortString()} -> {push.Update.NewImage.ToShortString()}");
            }
            var commit = await _git.Commit(repoDir.Path, commitTitle, description: commitDescriptionSb.ToString(), all: true);
            await _redis.SetAddAsync(RedisNamespacer.Git.Commits, commit);

            var gitHost = gitHostServiceProvider.GetGitHost(host);
            if (repositoryConfiguration.CreateMergeRequestsForPushes)
            {
                var updateBranchName = GetUpdatesBranchName(targetBranchName);
                _logger.LogInformation("Creating merge request for push. I will use updates branch {Branch} and target branch {Target}", updateBranchName, targetBranchName);
                await _git.PushAsync(host, repositoryConfiguration, repoDir.Path, updateBranchName, force: true);
                if (!await gitHost.HasOpenMergeRequestsForBranch(repositoryConfiguration, updateBranchName, cancellationToken))
                {
                    _logger.LogInformation("No open merge request(s) for {Branch} found, I will create a new one", updateBranchName);
                    var mergeRequestUrl = await gitHost.CreateMergeRequestForBranch(repositoryConfiguration, host, updateBranchName, targetBranchName, cancellationToken);
                    _logger.LogInformation("Created a merge request at {MergeRequestUrl}", mergeRequestUrl);
                }
                else
                {
                    _logger.LogInformation("Found existing open merge request(s) for {Branch}", updateBranchName);
                }
            }
            else
            {
                _logger.LogInformation("Pushing updates to {Branch}", targetBranchName);
                var hasUpstream = await _git.CheckIfHasUpstreamAsync(repoDir.Path);
                try
                {
                    await _git.PushAsync(host, repositoryConfiguration, repoDir.Path, setUpstream: hasUpstream ? null : "origin");
                }
                catch (CommandExecutionException ex) when (Regex.IsMatch(ex.Result.StdErr.Or(""), @".*\[rejected\] +[ \S]*\(fetch first\).*"))
                {
                    _logger.LogInformation("Received a fetch first error for {Branch}, retrying with a rebase first", targetBranchName);
                    try
                    {
                        await _git.PullAsync(repoDir.Path, rebase: true);

                    }
                    catch (CommandExecutionException ex2) when (Regex.IsMatch(ex2.Result.StdOut.Or(""), @".*\bCONFLICT\b.*\bMerge conflict\b.*"))
                    {
                        _logger.LogError("Failed to rebase branch {Branch} due to merge conflict", targetBranchName);
                        throw;
                    }
                    await _git.PushAsync(host, repositoryConfiguration, repoDir.Path, setUpstream: hasUpstream ? null : "origin");
                }
            }

            // since we have already pushed the change, we can't really fail out of this
            // the cache will reach consistency with the upstream on subsequent runs so
            // we can safely discard these errors
            foreach (var push in scheduledPushes)
            {
                try
                {
                    await CompletePushAsync(push);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ran into exception while completing push for push {Push}: {ExceptionMessage}", push.Target.ToString(), ex.Message);
                }
            }
        }

    }
}
