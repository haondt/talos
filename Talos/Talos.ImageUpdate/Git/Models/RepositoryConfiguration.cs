namespace Talos.ImageUpdate.Git.Models
{
    public class RepositoryConfiguration
    {
        public required string Host { get; set; }
        public required string Url { get; set; }
        public string NormalizedUrl => Url.TrimEnd('/');

        public string? Branch { get; set; }
        public bool CreateMergeRequestsForPushes { get; set; } = false;
        public RepositoryGlobbingOptions Glob { get; set; } = new();
        public int CooldownSeconds { get; set; } = 0;
    }

    public class RepositoryGlobbingOptions
    {
        public RepositoryFileTypeGlobbingConfiguration? Dockerfile { get; set; }
        public RepositoryFileTypeGlobbingConfiguration? DockerCompose { get; set; }
    }

    public class RepositoryFileTypeGlobbingConfiguration
    {
        public List<string>? IncludeGlobs { get; set; }
        public List<string>? ExcludeGlobs { get; set; }
    }
}
