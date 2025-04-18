using StackExchange.Redis;

namespace Talos.ImageUpdate.Redis.Services
{
    public interface IRedisProvider
    {

        IDatabase GetDatabase(int db = -1);
        IDatabase GetDefaultDatabase();
    }
}