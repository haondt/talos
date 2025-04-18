using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using Talos.Core.Models;
using Talos.ImageUpdate.ImageUpdating.Models;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Shared.Constants;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.ImageUpdating.Services
{
    public class ImageUpdateDataRepository(IRedisProvider redisProvider, ILogger<ImageUpdateDataRepository> logger) : IImageUpdateDataRepository
    {
        private readonly IDatabase _redis = redisProvider.GetDefaultDatabase();
        public Task<bool> ClearImageUpdateDataCacheAsync(UpdateIdentity id)
        {
            return _redis.KeyDeleteAsync(RedisNamespacer.UpdateTarget(id.ToString()));
        }

        public async Task<Result<ImageUpdateData>> TryGetImageUpdateDataAsync(UpdateIdentity id)
        {
            var cachedResponse = await _redis.StringGetAsync(RedisNamespacer.UpdateTarget(id.ToString()));
            if (cachedResponse.IsNull)
                return new();
            var deserialized = JsonConvert.DeserializeObject<ImageUpdateData>(cachedResponse.ToString(), SerializationConstants.SerializerSettings);
            if (deserialized == null)
            {
                logger.LogWarning("Failed to parse stored json for image update data {Image}.", id);
                return new();
            }

            return deserialized;
        }

        public Task<bool> SetImageUpdateDataAsync(UpdateIdentity id, ImageUpdateData data)
        {
            var serialized = JsonConvert.SerializeObject(data, SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to serialize image update data for image {id}");
            return _redis.StringSetAsync(RedisNamespacer.UpdateTarget(id.ToString()), serialized);
        }
    }
}
