using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Abstractions;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class PushQueueMutator(
        IRedisProvider redisProvider, IOptions<UpdateThrottlingSettings> settings,
        ITracer<PushQueueMutator> tracer,
        ILogger<PushQueueMutator> _logger) : IPushQueueMutator
    {

        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private readonly IDatabase _queueDb = redisProvider.GetDatabase(settings.Value.RedisDatabase);

        public async Task UpsertAndEnqueuePushAsync(ScheduledPush push)
        {
            await _queueLock.WaitAsync();
            try
            {
                await UnsafeUpsertAndEnqueuePushAsync(push);
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public async Task UnsafeUpsertAndEnqueuePushAsync(ScheduledPush push)
        {
            using var span = tracer.StartSpan(nameof(UnsafeUpsertAndEnqueuePushAsync));
            var key = RedisNamespacer.Pushes.Push(push.Target.ToString());
            var value = JsonConvert.SerializeObject(push, SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to serialize scheduled push for target {key}");

            var existing = await _queueDb.StringGetAsync(key);
            if (!existing.IsNull)
            {
                var deserialized = JsonConvert.DeserializeObject<ScheduledPush>(existing.ToString(), SerializationConstants.SerializerSettings);
                if (deserialized.Update.NewImageCreatedOn > push.Update.NewImageCreatedOn)
                {
                    _logger.LogInformation("Skipping enqueue for push {ScheduledPush} as there is one enqueued already with a newer update ({ProposedCreatedOn} vs {ExistingCreatedOn})",
                        key, push.Update.NewImageCreatedOn, deserialized.Update.NewImageCreatedOn);
                    return;
                }
            }

            await _queueDb.StringSetAsync(key, value);
            await _queueDb.SetAddAsync(RedisNamespacer.Pushes.Queue, key);

            _logger.LogInformation("Enqueued push {ScheduledPush}", key);

        }

        public Task<long> GetDeadLetterQueueSizeAsync()
        {
            return _queueDb.SetLengthAsync(RedisNamespacer.Pushes.DeadLetters.Queue);
        }

        public async Task<long> ReplayDeadLettersAsync()
        {
            using var span = tracer.StartSpan(nameof(ReplayDeadLettersAsync));
            long total = 0;
            await _queueLock.WaitAsync();
            try
            {
                var deadLetterKeys = await _queueDb.SetMembersAsync(RedisNamespacer.Pushes.DeadLetters.Queue);
                foreach (var deadLetterKey in deadLetterKeys)
                {
                    var value = await _queueDb.StringGetAsync(deadLetterKey.ToString());
                    if (!value.IsNull)
                    {
                        var deadletter = JsonConvert.DeserializeObject<ScheduledPushDeadLetter>(value.ToString(), SerializationConstants.SerializerSettings);
                        await UnsafeUpsertAndEnqueuePushAsync(deadletter.Push);
                    }
                    await _queueDb.SetRemoveAsync(RedisNamespacer.Pushes.DeadLetters.Queue, deadLetterKey);
                    await _queueDb.KeyDeleteAsync(deadLetterKey.ToString());
                    total++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error while replaying deadletter queue: {ErrorMessage}", ex.Message);
            }
            finally
            {
                _queueLock.Release();
            }
            return total;
        }

        public async Task<long> ClearDeadLettersAsync()
        {
            using var span = tracer.StartSpan(nameof(ClearDeadLettersAsync));
            long total = 0;
            try
            {
                var deadLetterKeys = await _queueDb.SetMembersAsync(RedisNamespacer.Pushes.DeadLetters.Queue);
                foreach (var deadLetterKey in deadLetterKeys)
                {
                    await _queueDb.SetRemoveAsync(RedisNamespacer.Pushes.DeadLetters.Queue, deadLetterKey);
                    await _queueDb.KeyDeleteAsync(deadLetterKey.ToString());
                    total++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error while clearing deadletter queue: {ErrorMessage}", ex.Message);
            }
            return total;
        }

        public async Task AcquireQueueLock()
        {
            await _queueLock.WaitAsync();
        }

        public void ReleaseQueueLock()
        {
            _queueLock.Release();
        }
    }
}
