using Newtonsoft.Json;

namespace Talos.Api.Models
{
    public class GitLabPipelineEventDto : PipelineEventDto
    {
        public bool IsChildPipeline => ObjectAttributes.Source == GitLabConstants.PIPELINE_SOURCE_PARENT_PIPELINE;
        public bool IsCompleted => IsSuccess || IsFailed;
        public bool IsSuccess => ObjectAttributes.Status == GitLabConstants.PIPELINE_STATUS_SUCCESS;
        public bool IsFailed => ObjectAttributes.Status == GitLabConstants.PIPELINE_STATUS_FAILED;

        [JsonRequired]
        public required GitLabObjectAttributesDto ObjectAttributes { get; set; }

        [JsonRequired]
        public required GitLabProjectDto Project { get; set; }
        [JsonRequired]
        public required GitLabCommitDto Commit { get; set; }
        [JsonRequired]
        public required string ObjectKind { get; set; }
    }



    public class GitLabProjectDto
    {

        public required string Id { get; set; }
        public required string GitHttpUrl { get; set; }
        [JsonRequired]
        public required string PathWithNamespace { get; set; }
    }

    public class GitLabObjectAttributesDto
    {
        [JsonRequired]
        public required string Id { get; set; }
        [JsonRequired]
        public required string Source { get; set; }
        [JsonRequired]
        public required string Status { get; set; }
        [JsonRequired]
        public required string Url { get; set; }
        [JsonRequired]
        public required string Sha { get; set; }

        public int? Duration { get; set; }
    }

    public class GitLabCommitDto
    {
        [JsonRequired]
        public required string Id { get; set; }
        public string TruncatedId => Id[..8];
        public required string Url { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
    }
}
