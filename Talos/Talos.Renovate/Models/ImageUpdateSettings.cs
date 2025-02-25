using Microsoft.Extensions.Options;

namespace Talos.Renovate.Models
{
    public class ImageUpdateSettings
    {
        public Dictionary<string, HostConfiguration> Hosts { get; set; } = [];

        public List<RepositoryConfiguration> Repositories { get; set; } = [];

        public static OptionsBuilder<ImageUpdateSettings> Validate(OptionsBuilder<ImageUpdateSettings> builder)
        {
            builder.Validate(o => o.Repositories.All(r => !string.IsNullOrEmpty(r.Host)), "Repository host may not be empty.");
            builder.Validate(o => o.Repositories.All(r => !string.IsNullOrEmpty(r.Url)), "Repository url may not be empty.");

            builder.Validate(o =>
            {
                var visited = new HashSet<(string, string)>();
                foreach (var repository in o.Repositories)
                {
                    var entry = (repository.Host, repository.Url);
                    if (!visited.Add(entry))
                        return false;
                }

                return true;
            }, "Repositories must be unique by (Host, Url).");

            builder.Validate(o => o.Repositories.All(r => !string.IsNullOrEmpty(r.Schedule)), "Repository cannot have an empty schedule");
            builder.Validate(o => o.Repositories.All(r => o.Hosts.ContainsKey(r.Host)), "Repository cannot refer to an undefined host.");

            return builder;
        }
    }

    public class RepositoryConfiguration
    {
        public required string Host { get; set; }
        public required string Url { get; set; }
        public string? Branch { get; set; }
        public List<string>? IncludeGlobs { get; set; }
        public List<string>? ExcludeGlobs { get; set; }
        public required string Schedule { get; set; }
        public bool CreateMergeRequestsForPushes { get; set; } = false;
    }

    public class HostConfiguration
    {
        public HostType Type { get; set; } = HostType.Unknown;
        public string? Token { get; set; }
        public string? Username { get; set; }
    }

    public enum HostType
    {
        Unknown,
        GitLab
    }
}