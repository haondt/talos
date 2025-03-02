using StackExchange.Redis;

namespace Talos.Renovate.Abstractions
{
    public interface IRedisProvider
    {

        IDatabase GetDatabase(int db = -1);
        IDatabase GetDefaultDatabase();
        IServer GetServer();
    }
}