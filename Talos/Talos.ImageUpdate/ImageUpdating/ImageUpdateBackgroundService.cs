using Haondt.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Core.Abstractions;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.ImageUpdating.Models;
using Talos.ImageUpdate.ImageUpdating.Services;
using Talos.ImageUpdate.Repositories.Shared.Services;
using Talos.ImageUpdate.UpdatePushing.Models;
using Talos.ImageUpdate.UpdatePushing.Services;

namespace Talos.ImageUpdate.ImageUpdating
{
    public class ImageUpdateBackgroundService(
        IOptions<ImageUpdateSettings> updateOptions,
        ITracer<ImageUpdateBackgroundService> tracer,
        ILogger<ImageUpdateBackgroundService> logger,
        IRepositoryService repositoryService,
        IPushQueueMutator pushQueue,
        IImageUpdaterService imageUpdaterService
        ) : BackgroundService
    {

        private int _isRunning = 0;
        private readonly ImageUpdateSettings _updateSettings = updateOptions.Value;
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            switch (_updateSettings.Schedule.Type)
            {
                case ScheduleType.Delay:
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        using (var span = tracer.StartSpan(nameof(ExecuteAsync), SpanKind.Producer))
                            try
                            {
                                if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                                    throw new InvalidOperationException("An image update run is already in progress, please try again later.");
                                try
                                {
                                    using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
                                    logger.LogInformation("Starting image update run.");

                                    var tasks = _updateSettings.Repositories.Select(async r =>
                                    {
                                        try
                                        {
                                            using (logger.BeginScope(new Dictionary<string, object>
                                            {
                                                ["Host"] = r.Host,
                                            }))
                                            {
                                                var host = _updateSettings.Hosts[r.Host];
                                                await UpdateRepositoryAsync(host, r, cancellationToken);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "Image update on repository {RepositoryUrl} failed due to exception: {ExceptionMessage}", r.Url, ex.Message);
                                        }
                                    });

                                    await Task.WhenAll(tasks);
                                    logger.LogInformation("Image update run complete.");
                                    span.SetStatusSuccess();
                                }
                                finally
                                {
                                    Interlocked.Exchange(ref _isRunning, 0);
                                }
                            }
                            catch (Exception ex)
                            {
                                span.SetStatusFailure(ex.Message);
                                logger.LogError(ex, "Error during image update. Retrying after delay.");
                            }
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(_updateSettings.Schedule.DelaySeconds), cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown {nameof(ScheduleType)}: {_updateSettings.Schedule.Type}");
            }
        }

        private async Task UpdateRepositoryAsync(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, CancellationToken? cancellationToken = null)
        {
            using var span = tracer.StartSpan($"{nameof(UpdateRepositoryAsync)}");
            span.SetAttribute("Host", repositoryConfiguration.Host);

            var targets = await repositoryService.ExtractTargetsAsync(host, repositoryConfiguration);

            var processingTasks = targets.Select(async q =>
            {
                try
                {
                    return await imageUpdaterService.HandleImageUpdateAsync(q.Id, q.Location);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Processing update for image {Image} failed due to exception: {ExceptionMessage}", q.Id, ex.Message);
                    return new Optional<ScheduledPushWithIdentity>();
                }
            });

            var scheduledPushes = (await Task.WhenAll(processingTasks))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            if (scheduledPushes.Count == 0)
                return;

            // if any pushes are failed to be scheduled, its fine, we are continually
            // scheduling them anyways so they'll get scheduled again on next run
            List<Exception> exceptions = [];
            foreach (var push in scheduledPushes)
            {
                try
                {
                    await pushQueue.UpsertAndEnqueuePushAsync(push);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Enqueueing push {Push} failed due to exception: {Exception}", push.Identity, ex.Message);
                    exceptions.Add(ex);
                }
            }
            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);

        }
    }
}
