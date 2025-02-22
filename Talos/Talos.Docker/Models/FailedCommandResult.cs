using Haondt.Core.Models;

namespace Talos.Docker.Models
{
    public record struct FailedCommandResult(
        string Command,
        string Arguments,
        TimeSpan Duration,
        bool WasTimedOut,
        bool WasKilled,
        Optional<int> ExitCode = default,
        Optional<string> StdErr = default)
    {
    }
}
