using Haondt.Core.Models;

namespace Talos.Integration.Command.Models
{
    public readonly record struct CommandResult(
        string Command,
        string Arguments,
        TimeSpan Duration,
        int ExitCode,
        Optional<string> StdOut = default)
    {
    }

}
