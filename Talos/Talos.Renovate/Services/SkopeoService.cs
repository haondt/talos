using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
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
        private readonly TimeSpan _cacheDuration;
        private readonly string _keyPrefix;

        public SkopeoService(
            IOptions<SkopeoSettings> options,
            IRedisProvider redisProvider,
            ICommandFactory commandFactory)
        {
            _commandFactory = commandFactory;
            _settings = options.Value;
            _redis = redisProvider.GetDatabase(_settings.RedisDatabase);
            _cacheDuration = TimeSpan.FromHours(options.Value.CacheDurationHours);
            _keyPrefix = $"{nameof(SkopeoService)}";
        }

        public async Task<List<string>> ListTags(string image, CancellationToken? cancellationToken = null)
        {
            var response = await PerformSkopeoOperation("list-tags", image, cancellationToken);
            var deserialized = JsonConvert.DeserializeObject<SkopeoListTagsResponse>(response)
                ?? throw new JsonSerializationException($"Failed to deserialize skopeo response for get-tags {image}");
            return deserialized.Tags;
        }

        public async Task<SkopeoInspectResponse> Inspect(string image, CancellationToken? cancellationToken = null)
        {
            var response = await PerformSkopeoOperation("list-tags", image, cancellationToken);
            var deserialized = JsonConvert.DeserializeObject<SkopeoInspectResponse>(response)
                ?? throw new JsonSerializationException($"Failed to deserialize skopeo response for inspect {image}");
            return deserialized;
        }

        private async Task<string> PerformSkopeoOperation(string operation, string image, CancellationToken? cancellationToken = null)
        {
            var cacheKey = $"{_keyPrefix}:{operation}:{image}";

            var cachedResponse = await _redis.StringGetAsync(cacheKey);
            if (!cachedResponse.IsNull)
                return cachedResponse.ToString();

            var output = await _commandFactory.Create(_settings.SkopeoCommand)
                .WithArguments(a => a
                    .AddRange(_settings.SkopeoArguments)
                    .Add(operation)
                    .Add(image))
                .ExecuteAndCaptureStdoutAsync(cancellationToken);

            await _redis.StringSetAsync(cacheKey, output, _cacheDuration);
            return output;
        }

    }
}
