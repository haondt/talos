using StackExchange.Redis;
using Talos.Core.Abstractions;
using Talos.Core.Extensions;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class RedisProvider(ConnectionMultiplexer connectionMultiplexer,
       int defaultDb,
       ITracer<RedisProvider> tracer) : IRedisProvider
    {
        public IDatabase GetDatabase(int db = -1) => connectionMultiplexer.GetDatabase(db).WithMethodTracing(tracer);

        public IDatabase GetDefaultDatabase() => GetDatabase(defaultDb).WithMethodTracing(tracer);

        public IServer GetServer() => connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First()).WithMethodTracing(tracer);
    }
}
