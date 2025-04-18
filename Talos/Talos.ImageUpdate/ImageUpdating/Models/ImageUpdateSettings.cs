using Microsoft.Extensions.Options;
using Talos.ImageUpdate.Git.Models;

namespace Talos.ImageUpdate.ImageUpdating.Models
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
                    if (!visited.Add((repository.NormalizedUrl, repository.Branch)))
                        return false;

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

}