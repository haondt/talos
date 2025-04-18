using Talos.ImageUpdate.GitHosts.Shared.Models;

namespace Talos.ImageUpdate.Git.Models
{
    public class HostConfiguration
    {
        public HostType Type { get; set; } = HostType.Unknown;
        public string? Token { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
    }
}
