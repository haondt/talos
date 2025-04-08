using Microsoft.Extensions.Options;

namespace Talos.Renovate.Models
{
    public class ImageUpdateSettings
    {
        public Dictionary<string, HostConfiguration> Hosts { get; set; } = [];

        public List<RepositoryConfiguration> Repositories { get; set; } = [];

        public ScheduleSettings Schedule { get; set; } = new();

        public static OptionsBuilder<ImageUpdateSettings> Validate(OptionsBuilder<ImageUpdateSettings> builder)
        {
            builder.Validate(o => o.Repositories.All(r => !string.IsNullOrEmpty(r.Host)), "Repository host may not be empty.");
            builder.Validate(o => o.Repositories.All(r => !string.IsNullOrEmpty(r.Url)), "Repository url may not be empty.");

            builder.Validate(o =>
            {
                var visited = new HashSet<(string, string?)>();
                foreach (var repository in o.Repositories)
                {
                    if (!visited.Add((repository.NormalizedUrl, repository.Branch)))
                        return false;
                }

                return true;
            }, "Repositories must be unique by url and branch.");

            builder.Validate(o => o.Repositories.All(r => o.Hosts.ContainsKey(r.Host)), "Repository cannot refer to an undefined host.");

            return builder;
        }
    }

    public class ScheduleSettings
    {
        public ScheduleType Type { get; set; } = ScheduleType.Delay;
        public int DelaySeconds { get; set; } = 3600;
    }

    public enum ScheduleType
    {
        Delay
    }

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

    public class HostConfiguration
    {
        public HostType Type { get; set; } = HostType.Unknown;
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    public enum HostType
    {
        Unknown,
        GitLab
    }
}