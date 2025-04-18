using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Talos.ImageUpdate.Git;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.GitHosts.GitLab.Services;
using Talos.ImageUpdate.GitHosts.Shared.Services;
using Talos.ImageUpdate.ImageParsing;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.ImageUpdating;
using Talos.ImageUpdate.ImageUpdating.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Redis.Models;
using Talos.ImageUpdate.Redis.Services;
using Talos.ImageUpdate.Skopeo.Models;
using Talos.ImageUpdate.Skopeo.Services;
using Talos.ImageUpdate.UpdatePushing.Models;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.ImageUpdate.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTalosImageUpdateServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ISkopeoService, SkopeoService>();
            services.Configure<SkopeoSettings>(configuration.GetSection(nameof(SkopeoSettings)));
            SkopeoSettings.Validate(services.AddOptions<SkopeoSettings>()).ValidateOnStart();

            services.AddSingleton<IImageUpdaterService, ImageUpdaterService>();
            services.AddSingleton<IImageUpdateDataRepository, ImageUpdateDataRepository>();
            services.Configure<ImageUpdateSettings>(configuration.GetSection(nameof(ImageUpdateSettings)));
            ImageUpdateSettings.Validate(services.AddOptions<ImageUpdateSettings>()).ValidateOnStart();

            services.Configure<RedisSettings>(configuration.GetSection(nameof(RedisSettings)));
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
