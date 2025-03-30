using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;
using Talos.Renovate.Services;

namespace Talos.Renovate.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosRenovateServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ISkopeoService, SkopeoService>();
            services.AddSingleton<IImageUpdaterService, ImageUpdaterService>();
            services.AddSingleton<IImageUpdateDataRepository, ImageUpdateDataRepository>();
            services.Configure<ImageUpdateSettings>(configuration.GetSection(nameof(ImageUpdateSettings)));
            ImageUpdateSettings.Validate(services.AddOptions<ImageUpdateSettings>()).ValidateOnStart();
            services.Configure<RedisSettings>(configuration.GetSection(nameof(RedisSettings)));
            services.Configure<SkopeoSettings>(configuration.GetSection(nameof(SkopeoSettings)));
            SkopeoSettings.Validate(services.AddOptions<SkopeoSettings>()).ValidateOnStart();

            services.AddSingleton<IRedisProvider>(sp =>
            {
                var redisSettings = sp.GetRequiredService<IOptions<RedisSettings>>();
                var cm = ConnectionMultiplexer.Connect(redisSettings.Value.Endpoint);
                return ActivatorUtilities.CreateInstance<RedisProvider>(sp, cm, redisSettings.Value.DefaultDatabase);
            });

            services.AddSingleton<IGitHostServiceProvider, GitHostServiceProvider>();

            services.AddHttpClient<IGitHostService, GitLabService>();
            services.AddSingleton<IGitService, GitService>();
            services.Configure<GitSettings>(configuration.GetSection(nameof(GitSettings)));
            services.AddSingleton<IPushQueueMutator, PushQueueMutator>();
            services.Configure<UpdateThrottlingSettings>(configuration.GetSection(nameof(UpdateThrottlingSettings)));
            services.AddHostedService<PushQueueListener>();
            services.AddHostedService<ImageUpdateBackgroundService>();

            services.AddSingleton<IImageParser, ImageParser>();
            services.Configure<ImageParserSettings>(configuration.GetSection(nameof(ImageParserSettings)));


            return services;
        }
    }
}
