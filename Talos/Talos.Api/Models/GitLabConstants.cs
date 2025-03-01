namespace Talos.Api.Models
{
    public class GitLabConstants
    {
        public const string PIPELINE_SOURCE_PARENT_PIPELINE = "parent_pipeline";
        public const string PIPELINE_STATUS_FAILED = "failed";
        public const string PIPELINE_STATUS_SUCCESS = "success";
        public const string PIPELINE_KIND = "pipeline";
        public const string GITLAB_EVENT_PIPELINE = "Pipeline Hook";
        public const string GITLAB_EVENT_HEADER = "X-Gitlab-Event";
    }
}
