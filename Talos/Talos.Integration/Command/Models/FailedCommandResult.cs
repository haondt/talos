using Haondt.Core.Models;

namespace Talos.Integration.Command.Models
{
    public record FailedCommandResult
    {
        public required string Command { get; init; }
        public required string Arguments { get; init; }
        public TimeSpan Duration { get; init; }
        public bool WasTimedOut { get; init; }
        public bool WasKilled { get; init; }
        public bool WasCancelled { get; init; }
        public Optional<int> ExitCode { get; init; } = default;
        public Optional<string> StdErr { get; init; } = default;
        public Optional<string> StdOut { get; init; } = default;
    }
}
