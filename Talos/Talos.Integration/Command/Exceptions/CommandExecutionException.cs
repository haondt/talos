using System.Text;
using Talos.Integration.Command.Models;

namespace Talos.Integration.Command.Exceptions
{
    public class CommandExecutionException : Exception
    {
        private static string ComposeErrorMessage(FailedCommandResult result)
        {
            var sb = new StringBuilder();
            if (result.WasCancelled)
                sb.AppendLine("The process was cancelled.");
            else if (result.WasKilled)
                sb.AppendLine("The process was killed before it could finish.");
            else if (result.WasTimedOut)
                sb.AppendLine("The process was cancelled before it could finish.");
            else
                sb.AppendLine("Execution failed due to command result.");
            sb.AppendLine($"Command: {result.Command} {result.Arguments}");
            sb.AppendLine($"Duration: {result.Duration}");
            sb.AppendLine($"Timed out: {result.WasTimedOut}");
            sb.AppendLine($"Was killed: {result.WasKilled}");
            sb.AppendLine($"Was cancelled: {result.WasCancelled}");
            if (result.ExitCode.HasValue)
                sb.AppendLine($"Exit code: {result.ExitCode.Value}");
            if (result.StdErr.HasValue)
                sb.AppendLine($"StdErr: {result.StdErr.Value}");
            if (result.StdOut.HasValue)
                sb.AppendLine($"StdOut: {result.StdOut.Value}");

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
