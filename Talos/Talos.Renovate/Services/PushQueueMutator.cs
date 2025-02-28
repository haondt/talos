using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class PushQueueMutator(IRedisProvider redisProvider, IOptions<UpdateThrottlingSettings> settings, ILogger<PushQueueMutator> _logger) : IPushQueueMutator
    {

        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private readonly IDatabase _queueDb = redisProvider.GetDatabase(settings.Value.RedisDatabase);

        public async Task UpsertAndEnqueuePush(ScheduledPush push)
        {
            var key = RedisNamespacer.Pushes.Push(push.Target.ToString());
            var value = JsonConvert.SerializeObject(push, SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to serialize scheduled push for target {key}");
            await _queueLock.WaitAsync();
            try
            {
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
            finally
            {
                _queueLock.Release();
            }
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
