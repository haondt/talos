using Haondt.Core.Models;

namespace Talos.Docker.Models
{
    public struct CommandOptions
    {
        public string Command { get; set; }
        public Optional<TimeSpan> Timeout { get; set; }
        public Optional<TimeSpan> GracePeriod { get; set; }
    }
}
