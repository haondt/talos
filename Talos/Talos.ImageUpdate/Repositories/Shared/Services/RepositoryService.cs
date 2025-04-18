using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.RegularExpressions;
using Talos.Core.Extensions;
using Talos.Core.Models;
using Talos.ImageUpdate.Git;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.GitHosts.Shared.Services;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Repositories.Atomic.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;
using Talos.Integration.Command.Exceptions;

namespace Talos.ImageUpdate.Repositories.Shared.Services
{
    public class RepositoryService(
        ILogger<RepositoryService> logger,
        IGitService git,
        IGitHostServiceProvider gitHostServiceProvider,
        IImageUpdaterService imageUpdaterService,
        IRedisProvider redisProvider,
        IEnumerable<IRepositoryFileService> fileServices) : IRepositoryService
    {

        private readonly IDatabase _redis = redisProvider.GetDefaultDatabase();

        public async Task<List<(UpdateIdentity Id, IUpdateLocation Location)>> ExtractTargetsAsync(HostConfiguration host, RepositoryConfiguration repositoryConfiguration)
        {

            using var repoDir = await git.CloneAsync(host, repositoryConfiguration, depth: 1);

            var targets = new List<(UpdateIdentity Id, IUpdateLocation Location)>();
            var syncedTargets = new Dictionary<string, List<ISubatomicUpdateLocation>>();

            foreach (var result in fileServices.SelectMany(fs => fs.ExtractLocations(repositoryConfiguration, repoDir.Path)))
            {
                if (!result.IsSuccessful)
                {
                    logger.LogWarning("Failed to extract update target on repository {Repository} for image due to error {Error}.", repositoryConfiguration.NormalizedUrl, result.Reason);
                    continue;
                }

                if (result.Value.State.Configuration.Sync != null)
                {
                    if (result.Value is not ISubatomicUpdateLocation subatomicUpdateLocation)
                        throw new InvalidOperationException($"Cannot interpret location of type {result.Value.GetType()} at {result.Value.Coordinates} in repository {repositoryConfiguration.NormalizedUrl} as {typeof(ISubatomicUpdateLocationState)}.");


                    if (!syncedTargets.TryGetValue(result.Value.State.Configuration.Sync.Group, out var syncGroup))
                        syncGroup = syncedTargets[result.Value.State.Configuration.Sync.Group] = [];

                    syncGroup.Add(subatomicUpdateLocation);
                    continue;
                }

                targets.Add((result.Value.Coordinates.GetIdentity(repositoryConfiguration.NormalizedUrl, repositoryConfiguration.Branch.AsOptional()), result.Value));
            }


            foreach (var (group, groupTargets) in syncedTargets)
            {
                var parents = groupTargets.Where(t => t.State.Configuration.Sync!.Role == SyncRole.Parent).ToList();

                if (parents.Count > 1)
                {
                    logger.LogWarning("Failed to create synchronized update group {Group} due to multiple parents: {Parents}", group, parents.Select(q => q.Coordinates.GetIdentity(repositoryConfiguration.NormalizedUrl, repositoryConfiguration.Branch.AsOptional())).ToList());
                    continue;
                }

                if (parents.Count == 0)
                {
                    logger.LogWarning("Couldn't find a parent for group {Group}", group);
                    continue;
                }

                var parent = parents[0];
                var children = groupTargets.Where(t => t != parent)
                    .Select(q => q.State.Configuration.Sync!.Id).ToList();
                DetailedResult<string> groupValidityCheck = new();
                if (parent.State.Configuration.Sync!.Children != null)
                {
                    foreach (var desiredChild in parent.State.Configuration.Sync!.Children)
                        if (!children.Contains(desiredChild))
                        {
                            groupValidityCheck = DetailedResult<string>.Failure($"missing child {desiredChild}");
                            break;
                        }
                    foreach (var existingChild in children)
                        if (!parent.State.Configuration.Sync!.Children.Contains(existingChild))
                        {
                            groupValidityCheck = DetailedResult<string>.Failure($"found extra child {existingChild}");
                            break;
                        }
                }
                if (!groupValidityCheck.IsSuccessful)
                {
                    logger.LogWarning("Failed to create group {Group} due to expected child composition mismatch: {Reason}", group, groupValidityCheck.Reason);
                    continue;
                }

                var id = UpdateIdentity.Atomic(repositoryConfiguration.NormalizedUrl, repositoryConfiguration.Branch.AsOptional(), groupTargets.Select(q => q.Coordinates.GetIdentity(repositoryConfiguration.NormalizedUrl, repositoryConfiguration.Branch.AsOptional())));
                var location = AtomicUpdateLocation.Create(parent.State.Configuration, groupTargets);
                targets.Add((id, location));
            }

            return targets;
        }

        public async Task<(List<ScheduledPushWithIdentity> SuccessfulPushes, List<ScheduledPushDeadLetter> FailedPushes)> PushUpdates(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, List<ScheduledPushWithIdentity> scheduledPushes, CancellationToken? cancellationToken = null)
        {
            using var repoDir = await git.CloneAsync(host, repositoryConfiguration, depth: 1);

            // this will clone and checkout the target branch if its set, otherwise it will take the default branch
            var targetBranchName = await git.GetNameOfCurrentBranchAsync(repoDir.Path);
            if (repositoryConfiguration.CreateMergeRequestsForPushes)
                await git.CreateAndCheckoutBranch(repoDir.Path, GitConstants.GetUpdatesBranchName(targetBranchName));

            var deadletters = new List<ScheduledPushDeadLetter>();
            var stagedPushes = new List<(ScheduledPushWithIdentity Push, IUpdateLocationSnapshot Snapshot)>();

            foreach (var push in scheduledPushes)
            {
                var writeResult = push.Push.Writer.Write(repoDir.Path);
                if (!writeResult.IsSuccessful)
                {
                    deadletters.Add(new(push, writeResult.Reason));
                    continue;
                }
                stagedPushes.Add((push, writeResult.Value));
            }

            if (stagedPushes.Count == 0)
                return ([], deadletters);

            try
            {

                var commitTitle = "[Talos] Updating images";
                var commiteDescription = string.Join(Environment.NewLine, scheduledPushes.Select(q => q.Push.CommitMessage));
                var commit = await git.Commit(repoDir.Path, commitTitle, description: commiteDescription, all: true);
                await _redis.SetAddAsync(RedisNamespacer.Git.Commits, commit);

                var gitHost = gitHostServiceProvider.GetGitHost(host);
                if (repositoryConfiguration.CreateMergeRequestsForPushes)
                {
                    var updateBranchName = GitConstants.GetUpdatesBranchName(targetBranchName);
                    logger.LogInformation("Creating merge request for push. I will use updates branch {Branch} and target branch {Target}", updateBranchName, targetBranchName);
                    await git.PushAsync(host, repositoryConfiguration, repoDir.Path, updateBranchName, force: true);
                    if (!await gitHost.HasOpenMergeRequestsForBranch(repositoryConfiguration, updateBranchName, cancellationToken))
                    {
                        logger.LogInformation("No open merge request(s) for {Branch} found, I will create a new one", updateBranchName);
                        var mergeRequestUrl = await gitHost.CreateMergeRequestForBranch(repositoryConfiguration, host, updateBranchName, targetBranchName, cancellationToken);
                        logger.LogInformation("Created a merge request at {MergeRequestUrl}", mergeRequestUrl);
                    }
                    else
                    {
                        logger.LogInformation("Found existing open merge request(s) for {Branch}", updateBranchName);
                    }
                }
                else
                {
                    logger.LogInformation("Pushing updates to {Branch}", targetBranchName);
                    var hasUpstream = await git.CheckIfHasUpstreamAsync(repoDir.Path);
                    try
                    {
                        await git.PushAsync(host, repositoryConfiguration, repoDir.Path, setUpstream: hasUpstream ? null : "origin");
                    }
                    catch (CommandExecutionException ex) when (Regex.IsMatch(ex.Result.StdErr.Or(""), @".*\[rejected\] +[ \S]*\(fetch first\).*"))
                    {
                        logger.LogInformation("Received a fetch first error for {Branch}, retrying with a rebase first", targetBranchName);
                        try
                        {
                            await git.PullAsync(repoDir.Path, rebase: true);
                        }
                        catch (CommandExecutionException ex2) when (Regex.IsMatch(ex2.Result.StdOut.Or(""), @".*\bCONFLICT\b.*\bMerge conflict\b.*"))
                        {
                            logger.LogError("Failed to rebase branch {Branch} due to merge conflict", targetBranchName);
                            throw;
                        }
                        await git.PushAsync(host, repositoryConfiguration, repoDir.Path, setUpstream: hasUpstream ? null : "origin");
                    }
                }
            }
            catch (Exception ex)
            {
                return ([], deadletters.Concat(stagedPushes.Select(p => new ScheduledPushDeadLetter(p.Push, $"Exception thrown during git operations: {ex.Message}", ex.StackTrace.AsOptional()))).ToList());
            }

            // since we have already pushed the change, we can't really fail out of this
            // the cache will reach consistency with the upstream on subsequent runs so
            // we can safely discard these errors
            foreach (var push in stagedPushes)
            {
                try
                {
                    await imageUpdaterService.CompletePushAsync(push.Push, push.Snapshot);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ran into exception while completing push for push {Push}: {ExceptionMessage}", push.Push.Identity, ex.Message);
                }
            }
            return (stagedPushes.Select(p => p.Push).ToList(), deadletters);
        }

        public async Task<bool> CheckIfCommitBelongsToUs(string commit)
        {
            return await _redis.SetContainsAsync(RedisNamespacer.Git.Commits, commit);
        }


    }
}
