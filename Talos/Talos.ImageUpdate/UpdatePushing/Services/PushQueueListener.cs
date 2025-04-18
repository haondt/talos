using Haondt.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Diagnostics;
using Talos.Core.Abstractions;
using Talos.Core.Extensions;
using Talos.Core.Models;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.ImageUpdating.Models;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Repositories.Shared.Services;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.UpdatePushing.Services
{
    public class PushCapacityCalculator(UpdateThrottlingSettings Settings)
    {
        public Dictionary<string, Optional<int>> AvailablePushCapacityByDomain { get; set; } = [];

        public AbsoluteDateTime Now { get; set; } = AbsoluteDateTime.Now;
        public async Task<bool> TryConsumePushCapacityAsync(IReadOnlyDictionary<string, int> domains, IDatabase queueDb)
        {
            foreach (var (domain, count) in domains)
            {
                if (count == 0)
                    return true;

                if (!AvailablePushCapacityByDomain.TryGetValue(domain, out var capacity))
                {
                    if (!Settings.Domains.TryGetValue(domain, out var throttlingConfiguration))
                    {
                        AvailablePushCapacityByDomain[domain] = new();
                        continue;
                    }
                    var windowStart = Now with { UnixTimeSeconds = Now.UnixTimeSeconds - (int)throttlingConfiguration.Per };
                    var pushesInTheLast = await queueDb.SortedSetLengthAsync(RedisNamespacer.Pushes.Timestamps.Domain(domain), windowStart.UnixTimeSeconds, AbsoluteDateTime.MaxValue.UnixTimeSeconds);
                    capacity = AvailablePushCapacityByDomain[domain] = throttlingConfiguration.Limit - (int)pushesInTheLast;
                }

                if (!capacity.HasValue)
                    continue;

                if (capacity.Value < count)
                    return false;
            }

            foreach (var (domain, count) in domains)
                if (AvailablePushCapacityByDomain.TryGetValue(domain, out var capacity) && capacity.HasValue)
                    AvailablePushCapacityByDomain[domain] = capacity.Value - count;
            return true;
        }

        public Dictionary<string, ThrottlingConfiguration> GetThrottlingConfigurations(IEnumerable<string> domains)
        {
            return domains
                .Select(q => (q, Settings.Domains.TryGetValue(q, out var throttlingConfiguration), throttlingConfiguration))
                .Where(q => q.Item2)
                .ToDictionary(q => q.q, q => q.throttlingConfiguration!);
        }
    }

    public class PushQueueListener(IRedisProvider redisProvider,
        ITracer<IBatch> batchTracer,
        ITracer<PushQueueListener> tracer,
        IOptions<UpdateThrottlingSettings> settings,
        IOptions<ImageUpdateSettings> updateOptions,
        IRepositoryService repositoryService,
        ILogger<PushQueueMutator> _logger,
        IPushQueueMutator _throttlingService
        ) : BackgroundService
    {
        private readonly IDatabase _queueDb = redisProvider.GetDefaultDatabase();
        private const int REDIS_MAX_CHUNK_SIZE = 1000;

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

        private async Task<List<ScheduledPushWithIdentity>> GetPendingPushesAsync()
        {
            using var _ = tracer.StartSpan(nameof(GetPendingPushesAsync), traceLevel: TraceLevel.Verbose);
            var keys = await _queueDb.SetMembersAsync(RedisNamespacer.Pushes.Queue);
            var pushes = new List<ScheduledPushWithIdentity>();
            foreach (var key in keys)
            {
                var value = await _queueDb.StringGetAsync(key.ToString());
                if (!value.IsNull)
                    pushes.Add(JsonConvert.DeserializeObject<ScheduledPushWithIdentity>(value.ToString(), SerializationConstants.SerializerSettings));
            }
            return pushes;
        }

        private async Task CompletePushesAsync(IEnumerable<ScheduledPushWithIdentity> pushes, IEnumerable<ScheduledPushDeadLetter> deadletters, AbsoluteDateTime now)
        {
            foreach (var chunk in pushes.Chunk(REDIS_MAX_CHUNK_SIZE))
            {
                var batch = _queueDb.CreateBatch().WithMethodTracing(batchTracer);
                var tasks = new List<Task>();
                foreach (var push in chunk)
                {
                    var pushId = Guid.NewGuid().ToString();
                    foreach (var (domain, _) in push.Push.UpdatesPerDomain)
                        tasks.Add(batch.SortedSetAddAsync(RedisNamespacer.Pushes.Timestamps.Domain(domain), pushId, now.UnixTimeSeconds));
                    tasks.Add(batch.SortedSetAddAsync(RedisNamespacer.Pushes.Timestamps.Repo(push.Identity.GitRemoteUrl, push.Identity.GitBranch), pushId, now.UnixTimeSeconds));
                }
                batch.Execute();
                await Task.WhenAll(tasks);
            }

            var pushKeys = pushes.Select(p => (RedisValue)RedisNamespacer.Pushes.Push(p.Identity.ToString())).ToArray();
            await _queueDb.SetRemoveAsync(RedisNamespacer.Pushes.Queue, pushKeys);

            foreach (var chunk in pushKeys.Chunk(REDIS_MAX_CHUNK_SIZE))
            {
                var batch = _queueDb.CreateBatch().WithMethodTracing(batchTracer);
                var deleteTasks = chunk.Select(k => batch.KeyDeleteAsync(k.ToString())).ToList();
                batch.Execute();
                await Task.WhenAll(deleteTasks);
            }

            var deadletterKeys = deadletters.Select(p => (RedisValue)RedisNamespacer.Pushes.Push(p.Push.Identity.ToString())).ToArray();
            await _queueDb.SetRemoveAsync(RedisNamespacer.Pushes.Queue, deadletterKeys);

            foreach (var chunk in deadletters.Chunk(REDIS_MAX_CHUNK_SIZE))
            {
                var batch = _queueDb.CreateBatch().WithMethodTracing(batchTracer);
                var tasks = new List<Task>();
                foreach (var deadletter in chunk)
                {
                    var target = deadletter.Push.Identity;
                    var pushKey = RedisNamespacer.Pushes.Push(target.ToString());
                    var deadLetterKey = RedisNamespacer.Pushes.DeadLetters.DeadLetter(target.ToString());
                    tasks.Add(batch.StringSetAsync(deadLetterKey, JsonConvert.SerializeObject(deadletter, SerializationConstants.SerializerSettings)));
                    tasks.Add(batch.SetAddAsync(RedisNamespacer.Pushes.DeadLetters.Queue, deadLetterKey));
                    tasks.Add(batch.KeyDeleteAsync(pushKey));
                }
                batch.Execute();
                await Task.WhenAll(tasks);
            }
        }


        private (HostConfiguration Host, RepositoryConfiguration Repository) GetRepositoryConfiguration(string remoteUrl, Optional<string> branch)
        {
            var repositoryConfiguration = updateOptions.Value.Repositories.Single(r => r.NormalizedUrl == remoteUrl && r.Branch.AsOptional().IsEquivalentTo(branch));
            var hostConfiguration = updateOptions.Value.Hosts[repositoryConfiguration.Host];
            return (hostConfiguration, repositoryConfiguration);
        }
        private async Task ProcessPushesAsync(CancellationToken? cancellationToken = default)
        {
            var span = new Optional<ISpan>();
            await _throttlingService.AcquireQueueLock();
            try
            {
                var pushes = await GetPendingPushesAsync();
                if (pushes.Count == 0)
                    return;


                var calculator = new PushCapacityCalculator(settings.Value);
                var allowedPushesByDomain = new List<ScheduledPushWithIdentity>();
                var now = AbsoluteDateTime.Now;

                using (tracer.StartSpan(nameof(GetPendingPushesAsync), traceLevel: TraceLevel.Verbose))
                    foreach (var push in pushes)
                    {
                        var targetDomains = push.Push.UpdatesPerDomain;

                        if (targetDomains.Count == 0)
                            allowedPushesByDomain.Add(push);
                        else if (await calculator.TryConsumePushCapacityAsync(targetDomains, _queueDb))
                            allowedPushesByDomain.Add(push);
                    }

                if (allowedPushesByDomain.Count == 0)
                    return;

                span = new(tracer.StartSpan(nameof(ProcessPushesAsync)));
                using var _ = _logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.Value!.TraceId } });

                var pushesByRemote = allowedPushesByDomain
                    .GroupBy(p => (p.Identity.GitRemoteUrl, p.Identity.GitBranch))
                    .ToDictionary(p => p.Key, p => p.ToList());
                var allowedPushesByRemote = new List<(HostConfiguration host, RepositoryConfiguration repository, List<ScheduledPushWithIdentity>)>();

                foreach (var (remote, remotePushes) in pushesByRemote)
                {
                    var (host, repository) = GetRepositoryConfiguration(remote.GitRemoteUrl, remote.GitBranch);
                    if (repository.CooldownSeconds <= 0)
                    {
                        allowedPushesByRemote.Add((host, repository, remotePushes));
                        continue;
                    }

                    var windowStart = now with { UnixTimeSeconds = now.UnixTimeSeconds - (int)repository.CooldownSeconds };
                    var pushesInTheLast = await _queueDb.SortedSetLengthAsync(RedisNamespacer.Pushes.Timestamps.Repo(remote.GitRemoteUrl, remote.GitBranch), windowStart.UnixTimeSeconds, AbsoluteDateTime.MaxValue.UnixTimeSeconds);
                    if (pushesInTheLast > 0)
                        continue;

                    allowedPushesByRemote.Add((host, repository, remotePushes));
                }


                using (tracer.StartSpan("Push Updates"))
                    foreach (var (host, repository, allowedPushes) in allowedPushesByRemote)
                    {
                        if (cancellationToken?.IsCancellationRequested ?? false)
                            break;
                        try
                        {
                            var (success, deadletters) = await repositoryService.PushUpdates(host, repository, allowedPushes, cancellationToken);
                            await CompletePushesAsync(success, deadletters, now);
                            _logger.LogInformation("Processed {Count} pushes and {DeadLetters} deadletters for remote {Remote} [{Branch}]", success.Count, deadletters.Count, repository.NormalizedUrl, repository.Branch);
                        }
                        catch (Exception ex) when (ex is not TaskCanceledException)
                        {
                            _logger.LogError(ex, "Failed to process pushes for remote {Remote} due to exception: {ErrorMessage}", repository.NormalizedUrl, ex.Message);
                        }
                    }
            }
            catch (Exception ex) when (span.HasValue)
            {
                span.Value.SetStatusFailure(ex.Message);
                throw;
            }
            finally
            {
                try
                {
                    if (span.HasValue)
                        span.Value.Dispose();
                }
                finally
                {
                    _throttlingService.ReleaseQueueLock();
                }
            }

        }

    }
}
