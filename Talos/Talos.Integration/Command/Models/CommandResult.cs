using Haondt.Core.Models;

namespace Talos.Integration.Command.Models
{
    public readonly record struct CommandResult(
        string Command,
        string Arguments,
        TimeSpan Duration,
        Optional<string> StdOut = default)
    {
    }

}
