using Haondt.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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
        IDockerComposeFileService _dockerComposeFileService) : IImageUpdaterService
    {
        private int _isRunning = 0;

        private readonly record struct ScheduledPush(
            ImageUpdateIdentity Target,
            ImageUpdate Update)
        {

        }

        private readonly ImageUpdateSettings _updateSettings = updateOptions.Value;
        private readonly IDatabase _redis = redisProvider.GetDatabase(options.Value.RedisDatabase);
        public async Task RunAsync(CancellationToken? cancellationToken = null)
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


        private async Task RunAsync(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, CancellationToken? cancellationToken = null)
        {
            using var repoDir = CloneRepository(host, repositoryConfiguration);
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

            using var repo = new Repository(repoDir.Path);
            var targetBranchName = repositoryConfiguration.Branch ?? repo.Head.FriendlyName;
            if (repositoryConfiguration.CreateMergeRequestsForPushes)
                CreateAndCheckoutUpdatesBranch(repo, targetBranchName);

            foreach (var push in scheduledPushes)
            {
                var filePath = Path.Combine(repoDir.Path, push.Target.RelativeFilePath);
                var content = File.ReadAllText(filePath);
                var updatedContent = _dockerComposeFileService.SetServiceImage(content, push.Target.ServiceKey, push.Update.NewImage.ToString());
                File.WriteAllText(filePath, updatedContent);
            }

            CommitAllWithMessage(repo, "updating images");

            var gitHost = gitHostServiceProvider.GetGitHost(host);
            if (repositoryConfiguration.CreateMergeRequestsForPushes)
            {
                var updateBranchName = GetUpdatesBranchName(targetBranchName);
                _logger.LogInformation("Creating merge request for push. I will use updates branch {Branch} and target branch {Target}", updateBranchName, targetBranchName);
                ForcePushUpdatesBranch(repo, host, targetBranchName);
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

                var pushOptions = new PushOptions
                {
                    CredentialsProvider = GetCredentialsHandler(host)
                };
                try
                {
                    repo.Network.Push(repo.Head, pushOptions);
                }
                catch (NonFastForwardException)
                {
                    // TODO
                }
            }

            throw new NotImplementedException("we're getting there");
        }

    }
}
