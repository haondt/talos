using Haondt.Core.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using Talos.Core.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Domain.Services
{
    public class WebhookAuthenticationService(
        IOptions<RedisSettings> redisSettings,
        IRedisProvider redisProvider) : IWebHookAuthenticationService
    {
        private const int API_KEY_LENGTH_BITS = 512;
        private readonly IDatabase _redis = redisProvider.GetDatabase(redisSettings.Value.DefaultDatabaase);
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<string> GenerateApiTokenAsync(string name)
        {
            await _lock.WaitAsync();
            try
            {
                var byName = RedisNamespacer.Webhooks.Tokens.ByName;
                var byValue = RedisNamespacer.Webhooks.Tokens.ByValue;

                var existingApiKey = await _redis.HashGetAsync(byName, name);
                if (!existingApiKey.IsNull)
                    throw new ArgumentException($"There is already an existing api key with the name '{name}'");



                var keyBytes = RandomNumberGenerator.GetBytes(API_KEY_LENGTH_BITS / 8);
                var apiKey = Convert.ToBase64String(keyBytes)
                    .Replace('+', '-')
                    .Replace('/', '_');


                await _redis.HashSetAsync(byName, name, apiKey);
                await _redis.HashSetAsync(byValue, apiKey, name);

                return apiKey;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Result<string>> VerifyApiTokenAsync(string token)
        {
            var result = await _redis.HashGetAsync(RedisNamespacer.Webhooks.Tokens.ByValue, token);

            if (result.IsNull)
                return Result<string>.Failure;
            return result.ToString();
        }

        public async Task<List<string>> ListApiTokensAsync()
        {
            var byName = RedisNamespacer.Webhooks.Tokens.ByName;
            var keys = await _redis.HashKeysAsync(byName);
            return keys.Select(k => k.ToString()).ToList();
        }

        public async Task RevokeApiToken(string name)
        {
            var byName = RedisNamespacer.Webhooks.Tokens.ByName;
            var byValue = RedisNamespacer.Webhooks.Tokens.ByValue;

            await _lock.WaitAsync();
            try
            {
                var existingApiKey = await _redis.HashGetAsync(byName, name);
                if (existingApiKey.IsNull)
                    return;

                await _redis.HashDeleteAsync(byValue, existingApiKey.ToString());
                await _redis.HashDeleteAsync(byName, name);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
