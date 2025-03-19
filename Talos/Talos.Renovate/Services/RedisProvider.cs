using StackExchange.Redis;
using Talos.Core.Abstractions;
using Talos.Core.Extensions;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Services
{
    public class RedisProvider(ConnectionMultiplexer connectionMultiplexer,
       int defaultDb,
       ITracer<IDatabase> databaseTracer,
       ITracer<IServer> serverTracer) : IRedisProvider
    {
        public IDatabase GetDatabase(int db = -1) => connectionMultiplexer.GetDatabase(db).WithMethodTracing(databaseTracer);

        public IDatabase GetDefaultDatabase() => GetDatabase(defaultDb).WithMethodTracing(databaseTracer);

        public IServer GetServer() => connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First()).WithMethodTracing(serverTracer);
    }
}
