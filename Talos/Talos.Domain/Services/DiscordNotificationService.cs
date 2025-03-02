using Discord;
using System.Text;
using Talos.Discord.Services;
using Talos.Renovate.Abstractions;
using Talos.Renovate.Models;

namespace Talos.Domain.Services
{
    public class DiscordNotificationService : INotificationService
    {
        public async Task<string> CreateInteraction(ImageUpdate update)
        {
            return Guid.NewGuid().ToString();
        }

        public Task DeleteInteraction(string id)
        {
            return Task.CompletedTask;
        }

        public Task Notify(ImageUpdate update)
        {
            var descriptionSb = new StringBuilder("**Image Update Available**\n");
            descriptionSb.AppendLine($"-# {update.NewImage.ToShortString()} ({update.BumpSize.ToString().ToLower()})\n");
            descriptionSb.AppendLine($"Current Version\n```\n{update.PreviousImage.ToString()}\n```");
            descriptionSb.AppendLine($"New version, created {update.NewImageCreatedOn.LocalTime.ToString("g")}");
            descriptionSb.AppendLine($"```\n{update.NewImage.ToString()}\n```");

            var embed = new EmbedBuilder()
                .WithDescription(descriptionSb.ToString())
                .WithColor(Color.Purple)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            return ExternalDiscordEventChannelProvider.NotificationChannel.Writer.WriteAsync(new()
            {
                Embed = embed
            }).AsTask();

        }

        public Task Notify(PipelineCompletionEvent pipelineCompleted)
        {
            var descriptionSb = new StringBuilder("**Pipeline Completed**\n");
            descriptionSb.AppendLine($"-# {pipelineCompleted.RepositorySlug}\n");
            descriptionSb.AppendLine($"Pipeline [{pipelineCompleted.Id}]({pipelineCompleted.Url})");
            var statusSb = new StringBuilder($"{(pipelineCompleted.Status == PipelineStatus.Success ? "Succeeded" : "Failed")}");
            if (pipelineCompleted.Duration.HasValue)
                statusSb.Append($" in {pipelineCompleted.Duration.Value}");
            descriptionSb.AppendLine(statusSb.ToString());
            descriptionSb.AppendLine($"Commit [{pipelineCompleted.CommitShortSha}]({pipelineCompleted.CommitUrl})\n");
            if (pipelineCompleted.CommitTitle.HasValue)
                descriptionSb.AppendLine($"{pipelineCompleted.CommitTitle.Value}");
            if (pipelineCompleted.CommitMessage.HasValue)
                descriptionSb.AppendLine("> " + string.Join("\n> ", pipelineCompleted.CommitMessage.Value.Trim().Split('\n')));

            var embed = new EmbedBuilder()
                .WithDescription(descriptionSb.ToString())
                .WithColor(pipelineCompleted.Status == PipelineStatus.Success ? Color.Green : Color.Red)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            return ExternalDiscordEventChannelProvider.NotificationChannel.Writer.WriteAsync(new()
            {
                Embed = embed
            }).AsTask();
        }
    }
}
