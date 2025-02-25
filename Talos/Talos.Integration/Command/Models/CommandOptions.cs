using Haondt.Core.Models;

namespace Talos.Integration.Command.Models
{
    public readonly record struct CommandOptions
    {
        public string Command { get; init; }
        public Optional<IEnumerable<string>> SensitiveDataToMask { get; init; }
        public Optional<TimeSpan> Timeout { get; init; }
        public Optional<TimeSpan> GracePeriod { get; init; }
    }
}
