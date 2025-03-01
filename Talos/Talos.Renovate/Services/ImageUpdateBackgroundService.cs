using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Renovate.Services
{
    public class ImageUpdateBackgroundService(
        IOptions<ImageUpdateSettings> updateOptions,
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
                        try
                        {
                            await imageUpdaterService.RunUpdateAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
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
