using StackExchange.Redis;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class RedisProvider(ConnectionMultiplexer connectionMultiplexer,
       int defaultDb) : IRedisProvider
    {
        public IDatabase GetDatabase(int db = -1) => connectionMultiplexer.GetDatabase(db);

        public IDatabase GetDefaultDatabase() => GetDatabase(defaultDb);

        public IServer GetServer() => connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
    }
}
