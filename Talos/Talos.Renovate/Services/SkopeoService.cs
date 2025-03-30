using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Models;
using Talos.Integration.Command.Abstractions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Extensions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class SkopeoService : ISkopeoService
    {
        private readonly ICommandFactory _commandFactory;
        private readonly SkopeoSettings _settings;
        private readonly IDatabase _redis;
        private readonly Dictionary<string, SemaphoreSlim> _semaphores = new();
        private readonly object _semaphoreLock = new();
        private static readonly Random _random = new();

        public SkopeoService(
            IOptions<SkopeoSettings> options,
            IRedisProvider redisProvider,
            ICommandFactory commandFactory)
        {
            _commandFactory = commandFactory;
            _settings = options.Value;
            _redis = redisProvider.GetDatabase(_settings.RedisDatabase);
        }

        private TimeSpan GetCacheDuration()
        {
            var variance = (_random.NextDouble() * 2 - 1) * _settings.CacheDurationVarianceHours;
            var hours = _settings.CacheDurationHours + variance;
            return TimeSpan.FromHours(hours);
        }

        public async Task<List<string>> ListTags(string image, CancellationToken? cancellationToken = null)
        {
            var response = await PerformSkopeoOperation("list-tags", RedisNamespacer.Skopeo.Tags(image), image, cancellationToken);
            var deserialized = JsonConvert.DeserializeObject<SkopeoListTagsResponse>(response)
                ?? throw new JsonSerializationException($"Failed to deserialize skopeo response for get-tags {image}");
            return deserialized.Tags;
        }

        public async Task<SkopeoInspectResponse> Inspect(string image, CancellationToken? cancellationToken = null)
        {
            var response = await PerformSkopeoOperation("inspect", RedisNamespacer.Skopeo.Inspect(image), image, cancellationToken);
            var deserialized = JsonConvert.DeserializeObject<SkopeoInspectResponse>(response)
                ?? throw new JsonSerializationException($"Failed to deserialize skopeo response for inspect {image}");
            return deserialized;
        }

        private async Task<string> PerformSkopeoOperation(string operation, string cacheKey, string image, CancellationToken? cancellationToken = null)
        {

            var cachedResponse = await _redis.StringGetAsync(cacheKey);
            if (!cachedResponse.IsNull)
                return cachedResponse.ToString();

            // limit throughput to 1 request per cacheKey at a time
            SemaphoreSlim semaphore;
            lock (_semaphoreLock)
            {
                semaphore = _semaphores.TryGetValue(cacheKey, out var existingSemaphore)
                    ? existingSemaphore
                    : _semaphores[cacheKey] = new SemaphoreSlim(1, 1);
            }
            await semaphore.WaitAsync(cancellationToken ?? CancellationToken.None);

            try
            {
                cachedResponse = await _redis.StringGetAsync(cacheKey);
                if (!cachedResponse.IsNull)
                    return cachedResponse.ToString();

                var output = await _commandFactory.Create(_settings.SkopeoCommand)
                    .WithArguments(a => a
                        .AddRange(_settings.SkopeoArguments)
                        .Add(operation)
                        .Add($"docker://{image}"))
                    .ExecuteAndCaptureStdoutAsync(cancellationToken);

                await _redis.StringSetAsync(cacheKey, output, GetCacheDuration());
                return output;
            }
            finally
            {
                semaphore.Release();
                // clean up semaphore list
                lock (_semaphoreLock)
                {
                    if (semaphore.CurrentCount == 1)
                    {
                        _semaphores.Remove(cacheKey);
                    }
                }
            }
        }
    }
}
