using System.Text;
using Talos.Docker.Models;

namespace Talos.Docker.Exceptions
{
    public class CommandExecutionException : Exception
    {
        private static string ComposeErrorMessage(FailedCommandResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Execution failed due to command result.");
            sb.AppendLine($"Command: {result.Command} {result.Arguments}");
            sb.AppendLine($"Duration: {result.Duration}");
            sb.AppendLine($"Timed out: {result.WasTimedOut}");
            sb.AppendLine($"Was killed: {result.WasKilled}");
            sb.AppendLine($"Exit code: {result.ExitCode}");
            if (result.StdErr.HasValue)
                sb.AppendLine($"StdOut: {result.StdErr.Value}");

            return sb.ToString();
        }

        public FailedCommandResult Result { get; private init; }

        public CommandExecutionException(FailedCommandResult result) : this(result, null)
        {
        }

        public CommandExecutionException(FailedCommandResult result, Exception? innerException) : base(ComposeErrorMessage(result), innerException)
        {
            Result = result;
        }

    }
}
