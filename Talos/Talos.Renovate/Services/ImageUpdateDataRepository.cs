using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class ImageUpdateDataRepository(IRedisProvider redisProvider, ILogger<ImageUpdateDataRepository> _logger) : IImageUpdateDataRepository
    {
        private readonly IDatabase _redis = redisProvider.GetDefaultDatabase();
        public Task<bool> ClearImageUpdateDataCacheAsync(ImageUpdateIdentity id)
        {
            return _redis.KeyDeleteAsync(RedisNamespacer.UpdateTarget(id.ToString()));
        }

        public async Task<Optional<ImageUpdateData>> TryGetImageUpdateDataAsync(ImageUpdateIdentity id)
        {
            var cachedResponse = await _redis.StringGetAsync(RedisNamespacer.UpdateTarget(id.ToString()));
            if (cachedResponse.IsNull)
                return new();
            var deserialized = JsonConvert.DeserializeObject<ImageUpdateData>(cachedResponse.ToString(), SerializationConstants.SerializerSettings);
            if (deserialized == null)
            {
                _logger.LogWarning("Failed to parse stored json for image update data {Image}.", id);
                return new();
            }

            return deserialized;
        }

        public Task<bool> SetImageUpdateDataAsync(ImageUpdateIdentity id, ImageUpdateData data)
        {
            var serialized = JsonConvert.SerializeObject(data, SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to serialize image update data for image {id}");
            return _redis.StringSetAsync(RedisNamespacer.UpdateTarget(id.ToString()), serialized);
        }
    }
}
