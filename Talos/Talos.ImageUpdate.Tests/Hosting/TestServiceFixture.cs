using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Talos.Core.Abstractions;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Shared.Extensions;
using Talos.ImageUpdate.Skopeo.Services;
using Talos.ImageUpdate.Tests.Fakes;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.ImageUpdate.Tests.Hosting
{
    public class TestServiceFixture : IDisposable
    {
        public ServiceProvider ServiceProvider { get; }
        public IConfiguration Configuration { get; }

        public TestServiceFixture()
        {
            var configBuilder = new ConfigurationBuilder();
            //.SetBasePath(Directory.GetCurrentDirectory())
            //.AddJsonFile("appsettings.json");

            //configBuilder.AddEnvironmentVariables();

            Configuration = configBuilder.Build();

            var services = new ServiceCollection();

            //services.AddSingleton<IConfiguration>(Configuration);

            ConfigureServices(services);

            ServiceProvider = services.BuildServiceProvider();
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddTalosImageUpdateServices(Configuration);
            services.AddSingleton<INotificationService, FakeNotificationService>();
            services.AddSingleton<ISkopeoService, FakeSkopeoService>();
            services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
            services.AddSingleton(typeof(ITracer<>), typeof(FakeTracer<>));
            services.AddSingleton<IRedisProvider, FakeRedisProvider>();
        }

        public void Dispose()
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
