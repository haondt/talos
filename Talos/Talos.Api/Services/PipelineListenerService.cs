
using Haondt.Core.Models;
using Talos.Api.Models;
using Talos.Core.Abstractions;
using Talos.Core.Extensions;
using Talos.Domain.Models;
using Talos.Domain.Services;
using Talos.ImageUpdate.Repositories.Shared.Services;

namespace Talos.Api.Services
{
    public class PipelineListenerService(
        ITracer<PipelineListenerService> tracer,
        ITalosNotificationService notificationService,
        IRepositoryService repositoryService,
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
            using var span = tracer.StartSpan(nameof(ExecuteAsync), SpanKind.Consumer);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
            span.SetStatusSuccess();

            // discard child pipelines
            if (pipelineEvent.IsChildPipeline)
                return;

            // discard incomplete pipelines
            if (!pipelineEvent.IsCompleted)
                return;

            // discard pipelines instigated by other sources
            if (!await repositoryService.CheckIfCommitBelongsToUs(pipelineEvent.ObjectAttributes.Sha))
                return;

            try
            {
                var commitMessage = pipelineEvent.Commit.Message.AsOptional();
                if (!string.IsNullOrEmpty(pipelineEvent.Commit.Message))
                    if (!string.IsNullOrEmpty(pipelineEvent.Commit.Title))
                        if (pipelineEvent.Commit.Message.StartsWith(pipelineEvent.Commit.Title))
                            commitMessage = pipelineEvent.Commit.Message[pipelineEvent.Commit.Title.Length..];

                await notificationService.Notify(new PipelineCompletionEvent
                {
                    RepositorySlug = pipelineEvent.Project.PathWithNamespace,
                    CommitShortSha = pipelineEvent.Commit.TruncatedId,
                    CommitUrl = pipelineEvent.Commit.Url,
                    CommitTitle = pipelineEvent.Commit.Title.AsOptional(),
                    CommitMessage = commitMessage,
                    Duration = pipelineEvent.ObjectAttributes.Duration.HasValue
                        ? TimeSpan.FromSeconds(pipelineEvent.ObjectAttributes.Duration.Value)
                        : new Optional<TimeSpan>(),
                    Id = $"#{pipelineEvent.ObjectAttributes.Id}",
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
                span.SetStatusFailure(ex.GetType().ToString());
            }


        }
    }
}
