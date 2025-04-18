using Haondt.Core.Models;

namespace Talos.Domain.Models
{
    public class PipelineCompletionEvent
    {
        public required string Id { get; set; }
        public required string Url { get; set; }
        public required string RepositorySlug { get; set; }

        public required string CommitShortSha { get; set; }
        public required string CommitUrl { get; set; }
        public Optional<string> CommitTitle { get; set; }
        public Optional<string> CommitMessage { get; set; }

        public PipelineStatus Status { get; set; }

        public Optional<TimeSpan> Duration { get; set; }
    }

    public enum PipelineStatus
    {
        Success,
        Failed
    }
}
