using Haondt.Core.Models;

namespace Talos.Docker.Models
{
    public record struct FailedCommandResult(
        string Command,
        string Arguments,
        TimeSpan Duration,
        bool WasTimedOut,
        bool WasKilled,
        bool WasCancelled,
        Optional<int> ExitCode = default,
        Optional<string> StdErr = default)
    {
    }
}
