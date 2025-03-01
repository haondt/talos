using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class UpdateThrottlingQueueConsumer(IRedisProvider redisProvider,
        IOptions<UpdateThrottlingSettings> settings,
        ILogger<PushQueueMutator> _logger,
        IPushQueueMutator _throttlingService,
        IImageUpdaterService imageUpdaterService) : BackgroundService
    {
        private readonly IDatabase _queueDb = redisProvider.GetDatabase(settings.Value.RedisDatabase);

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPushesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "Failed to process pushes: {ErrorMessage}", ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(settings.Value.QueuePollingFrequencyInSeconds), cancellationToken);
            }
        }

        private async Task<List<ScheduledPush>> GetPendingPushesAsync()
        {
            var keys = await _queueDb.SetMembersAsync(RedisNamespacer.Pushes.Queue);
            var pushes = new List<ScheduledPush>();
            foreach (var key in keys)
            {
                var value = await _queueDb.StringGetAsync(key.ToString());
                if (!value.IsNull)
                    pushes.Add(JsonConvert.DeserializeObject<ScheduledPush>(value.ToString(), SerializationConstants.SerializerSettings));
            }
            return pushes;
        }

        private async Task CompletePushesAsync(IEnumerable<ScheduledPush> pushes, AbsoluteDateTime now)
        {
            var keys = pushes.Select(p => (RedisValue)RedisNamespacer.Pushes.Push(p.Target.ToString())).ToArray();
            foreach (var push in pushes)
            {
                var pushId = Guid.NewGuid().ToString();
                await _queueDb.SortedSetAddAsync(RedisNamespacer.Pushes.Timestamps.Domain(push.Update.NewImage.Domain.Or("")), pushId, now.UnixTimeSeconds);
                await _queueDb.SortedSetAddAsync(RedisNamespacer.Pushes.Timestamps.Repo(push.Target.GitRemoteUrl), pushId, now.UnixTimeSeconds);
            }
            await _queueDb.SetRemoveAsync(RedisNamespacer.Pushes.Queue, keys);
            foreach (var key in keys)
                await _queueDb.KeyDeleteAsync(key.ToString());
        }

        private async Task ProcessPushesAsync(CancellationToken? cancellationToken = default)
        {
            await _throttlingService.AcquireQueueLock();
            try
            {
                var pushes = await GetPendingPushesAsync();
                var pushesByDomain = pushes.GroupBy(p => p.Update.NewImage.Domain.Or(""))
                    .ToDictionary(g => g.Key, g => g.ToList());
                var allowedPushesByDomain = new List<ScheduledPush>();
                var now = AbsoluteDateTime.Now;

                foreach (var (domain, domainPushes) in pushesByDomain)
                {
                    if (string.IsNullOrEmpty(domain) || !settings.Value.Domains.TryGetValue(domain, out var throttlingConfiguration))
                        allowedPushesByDomain.AddRange(domainPushes);
                    else
                    {
                        var windowStart = now with { UnixTimeSeconds = now.UnixTimeSeconds - (int)throttlingConfiguration.Per };
                        var pushesInTheLast = await _queueDb.SortedSetLengthAsync(RedisNamespacer.Pushes.Timestamps.Domain(domain), windowStart.UnixTimeSeconds, AbsoluteDateTime.MaxValue.UnixTimeSeconds);
                        var available = throttlingConfiguration.Limit - (int)pushesInTheLast;
                        if (available > 0)
                            allowedPushesByDomain.AddRange(domainPushes.Take(available));
                    }
                }

                if (allowedPushesByDomain.Count == 0)
                    return;

                var pushesByRemote = allowedPushesByDomain
                    .GroupBy(p => p.Target.GitRemoteUrl)
                    .ToDictionary(p => p.Key, p => p.ToList());
                var allowedPushesByRemote = new List<(HostConfiguration host, RepositoryConfiguration repository, List<ScheduledPush>)>();

                foreach (var (remote, remotePushes) in pushesByRemote)
                {
                    var (host, repository) = imageUpdaterService.GetRepositoryConfiguration(remote);
                    if (repository.CooldownSeconds <= 0)
                    {
                        allowedPushesByRemote.Add((host, repository, remotePushes));
                        continue;
                    }

                    var windowStart = now with { UnixTimeSeconds = now.UnixTimeSeconds - (int)repository.CooldownSeconds };
                    var pushesInTheLast = await _queueDb.SortedSetLengthAsync(RedisNamespacer.Pushes.Timestamps.Repo(remote), windowStart.UnixTimeSeconds, AbsoluteDateTime.MaxValue.UnixTimeSeconds);
                    if (pushesInTheLast > 0)
                        continue;

                    allowedPushesByRemote.Add((host, repository, remotePushes));
                }


                foreach (var (host, repository, allowedPushes) in allowedPushesByRemote)
                {
                    if (cancellationToken?.IsCancellationRequested ?? false)
                        break;
                    try
                    {
                        await imageUpdaterService.PushUpdates(host, repository, allowedPushes, cancellationToken);
                        await CompletePushesAsync(allowedPushes, now);
                        _logger.LogInformation("Processed {Count} pushes for remote {Remote}", allowedPushes.Count, repository.NormalizedUrl);
                    }
                    catch (Exception ex) when (ex is not TaskCanceledException)
                    {
                        _logger.LogError(ex, "Failed to process pushes for remote {Remote} due to exception: {ErrorMessage}", repository.NormalizedUrl, ex.Message);
                    }
                }
            }
            finally
            {
                _throttlingService.ReleaseQueueLock();
            }

        }

    }
}
