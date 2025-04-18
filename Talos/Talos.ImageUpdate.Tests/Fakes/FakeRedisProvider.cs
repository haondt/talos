using Moq;
using StackExchange.Redis;
using Talos.ImageUpdate.Redis.Services;

namespace Talos.ImageUpdate.Tests.Fakes
{
    public class FakeRedisProvider : IRedisProvider
    {
        Mock<IDatabase> _mockDatabase = new();
        public FakeRedisProvider()
        {

        }

        public IDatabase GetDatabase(int db = -1)
        {
            return _mockDatabase.Object;
        }

        public IDatabase GetDefaultDatabase()
        {
            return _mockDatabase.Object;
        }

    }
}
