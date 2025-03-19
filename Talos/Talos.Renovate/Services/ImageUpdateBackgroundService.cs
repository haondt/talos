using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Core.Abstractions;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class ImageUpdateBackgroundService(
        IOptions<ImageUpdateSettings> updateOptions,
        ITracer<ImageUpdateBackgroundService> tracer,
        ILogger<ImageUpdateBackgroundService> logger,
        IImageUpdaterService imageUpdaterService) : BackgroundService
    {

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
                                using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
                                await imageUpdaterService.RunUpdateAsync(cancellationToken);
                                span.SetStatusSuccess();
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
    }
}
