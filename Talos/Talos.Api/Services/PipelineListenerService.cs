
using Haondt.Core.Models;
using Talos.Api.Extensions;
using Talos.Api.Models;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Api.Services
{
    public class PipelineListenerService(
        INotificationService notificationService,
        IImageUpdaterService imageUpdaterService,
        ILogger<PipelineListenerService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var next = await PipelineListenerChannelProvider.Channel.Reader.ReadAsync(stoppingToken);
                    await HandlePipelineEvent(next);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while listening to channel. Waiting 1 second before attempting to reconnect.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private Task HandlePipelineEvent(PipelineEventDto pipelineEvent)
        {
            if (pipelineEvent is GitLabPipelineEventDto gitlabEvent)
                return HandleGitLabPipelineEvent(gitlabEvent);
            return Task.CompletedTask;
        }

        private async Task HandleGitLabPipelineEvent(GitLabPipelineEventDto pipelineEvent)
        {
            // discard child pipelines
            if (pipelineEvent.IsChildPipeline)
                return;

            // discard incomplete pipelines
            if (!pipelineEvent.IsCompleted)
                return;

            // discard pipelines instigated by other sources
            if (!await imageUpdaterService.CheckIfCommitBelongsToUs(pipelineEvent.ObjectAttributes.Sha))
                return;

            try
            {
                await notificationService.Notify(new PipelineCompletionEvent
                {
                    CommitShortSha = pipelineEvent.Commit.TruncatedId,
                    CommitUrl = pipelineEvent.Commit.Url,
                    CommitTitle = pipelineEvent.Commit.Title.AsOptional(),
                    CommitMessage = pipelineEvent.Commit.Message.AsOptional(),
                    Duration = pipelineEvent.ObjectAttributes.Duration.HasValue
                        ? TimeSpan.FromSeconds(pipelineEvent.ObjectAttributes.Duration.Value)
                        : new Optional<TimeSpan>(),
                    Id = pipelineEvent.ObjectAttributes.Id,
                    Status = pipelineEvent.IsSuccess
                        ? PipelineStatus.Success
                        : PipelineStatus.Failed,
                    Url = pipelineEvent.ObjectAttributes.Url
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification service failed to accept pipeline completion event for gitlab pipeline {Pipeline} {Url}",
                    pipelineEvent.ObjectAttributes.Id, pipelineEvent.ObjectAttributes.Url);
            }


        }
    }
}
