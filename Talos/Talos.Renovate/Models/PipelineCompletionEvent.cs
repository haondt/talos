using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public class PipelineCompletionEvent
    {
        public required string Id { get; set; }
        public required string Url { get; set; }

        public required string CommitShortSha { get; set; }
        public required string CommitUrl { get; set; }

        public PipelineStatus Status { get; set; }

        public Optional<TimeSpan> Duration { get; set; }
    }

    public enum PipelineStatus
    {
        Success,
        Failed
    }
}
