using StackExchange.Redis;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class RedisProvider(ConnectionMultiplexer connectionMultiplexer) : IRedisProvider
    {
        public IDatabase GetDatabase(int db = -1) => connectionMultiplexer.GetDatabase(db);
        public IServer GetServer() => connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
    }
}
