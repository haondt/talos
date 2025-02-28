using CliWrap;
using CliWrap.Builders;
using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using Talos.Integration.Command.Models;
using CommandExecutionException = Talos.Integration.Command.Exceptions.CommandExecutionException;
using CommandResult = Talos.Integration.Command.Models.CommandResult;

namespace Talos.Integration.Command.Services
{
    public record CommandBuilder(
        CliWrap.Command Command,
        CommandOptions Options,
        CommandSettings Settings,
        ILogger<CommandBuilder> _logger)
    {
        public static CommandBuilder Wrap(string command, CommandSettings settings, ILogger<CommandBuilder> logger)
        {
            return new CommandBuilder(
                Cli.Wrap(command),
                new()
                {
                    Command = command
                },
                settings,
                logger);
        }

        public static string MaskSensitiveData(string target, IEnumerable<string> remove)
        {
            foreach (var text in remove)
                target = target.Replace(text, "<REDACTED>");

            return target;
        }

        [Pure]
        public CommandBuilder WithRawArguments(string arguments) => this with { Command = Command.WithArguments(arguments) };

        [Pure]
        public CommandBuilder WithArguments(Action<ArgumentsBuilder> arguments) => this with { Command = Command.WithArguments(arguments) };

        [Pure]
        public CommandBuilder WithTimeout(TimeSpan timeout) => this with { Options = Options with { Timeout = timeout } };

        [Pure]
        public CommandBuilder WithGracePeriod(TimeSpan timeout) => this with { Options = Options with { GracePeriod = timeout } };

        [Pure]
        public CommandBuilder WithoutTimeout() => this with { Options = Options with { Timeout = Timeout.InfiniteTimeSpan } };

        [Pure]
        public CommandBuilder WithSensitiveDataMasked(string sensitiveData)
        {
            var currentMask = Options.SensitiveDataToMask;
            return this with
            {
                Options = Options with
                {
                    SensitiveDataToMask = new(currentMask.HasValue
                        ? currentMask.Value.Append(sensitiveData)
                        : [sensitiveData])
                }
            };
        }

        [Pure]
        public CommandBuilder WithEnvironmentVariables(IReadOnlyDictionary<string, string> vars) => this with { Command = Command.WithEnvironmentVariables(vars!) };
        [Pure]
        public CommandBuilder WithWorkingDirectory(string directoryPath) => this with { Command = Command.WithWorkingDirectory(directoryPath) };

        [Pure]
        public CommandBuilder WithAllowedNonZeroExitCode() => this with { Command = Command.WithValidation(CommandResultValidation.None) };

        public async Task<string> ExecuteAndCaptureStdoutAsync(CancellationToken? cancellationToken = null)
        {
            var result = await ExecuteAsync(captureStdOut: true, cancellationToken: cancellationToken);
            return result.StdOut.Or("");
        }

        public async Task<string> ExecuteAndCaptureStdoutAsync(PipeTarget target, CancellationToken? cancellationToken = null)
        {
            var result = await ExecuteAsync(target, cancellationToken: cancellationToken);
            return result.StdOut.Or("");
        }

        public async Task<CommandResult> ExecuteAsync(PipeTarget? pipeStdOut = null, bool captureStdOut = false, CancellationToken? cancellationToken = null)
        {
            var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
            var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Command.Arguments, q)).Or(Command.Arguments);

            _logger.LogInformation("Executing command: {Command} {Arguments}",
                maskedCommand, maskedArguments);
            try
            {
                var result = await InternalExecuteAsync(pipeStdOut, captureStdOut, cancellationToken);
                _logger.LogInformation("Completed command: {Command} {Arguments} in {Result}",
                    maskedCommand, maskedArguments, result.Duration);

                return result;
            }
            catch (CommandExecutionException ex)
            {
                if (ex.Result.WasTimedOut)
                    _logger.LogWarning("Command {Command} {Arguments} timed out after {Duration}",
                        maskedCommand, maskedArguments, ex.Result.Duration);
                else if (ex.Result.WasKilled)
                    _logger.LogWarning("Command {Command} {Arguments} was killed after {Duration}",
                        maskedCommand, maskedArguments, ex.Result.Duration);
                else
                    if (ex.Result.ExitCode.HasValue)
                    _logger.LogError(ex, "Command {Command} {Arguments} failed with exit code {ExitCode}.",
                        maskedCommand, maskedArguments, ex.Result.ExitCode.Value);
                else
                    _logger.LogError(ex, "Command {Command} {Arguments} failed with no exit code.",
                        maskedCommand, maskedArguments);
                throw;
            }
        }

        private async Task<CommandResult> InternalExecuteAsync(PipeTarget? pipeStdOut = null, bool captureStdOut = false, CancellationToken? cancellationToken = null)
        {
            var stopwatch = Stopwatch.StartNew();

            var timeout = new Optional<TimeSpan>();
            if (!Options.Timeout.HasValue)
                timeout = TimeSpan.FromSeconds(Settings.DefaultTimeoutSeconds);
            else if (Options.Timeout.Value != Timeout.InfiniteTimeSpan && Options.Timeout.Value > TimeSpan.Zero)
                timeout = Options.Timeout.Value;

            var graceTimeout = new Optional<TimeSpan>();
            if (!Options.GracePeriod.HasValue)
                graceTimeout = TimeSpan.FromSeconds(Settings.DefaultGracePeriodSeconds);
            else if (Options.GracePeriod.Value != Timeout.InfiniteTimeSpan && Options.GracePeriod.Value > TimeSpan.Zero)
                graceTimeout = Options.GracePeriod.Value;

            var timeoutCts = new Optional<CancellationTokenSource>();
            var interruptCts = new Optional<CancellationTokenSource>();
            var killCts = new Optional<CancellationTokenSource>();
            if (timeout.HasValue)
            {
                timeoutCts = new CancellationTokenSource(timeout.Value);
                if (cancellationToken.HasValue)
                    interruptCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Value.Token, cancellationToken.Value);
                else
                    interruptCts = timeoutCts;
            }

            CliWrap.CommandResult result;
            var stdErrSb = new StringBuilder();
            var stdOutSb = new Optional<StringBuilder>();

            var command = Command.WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrSb));
            if (captureStdOut)
            {
                stdOutSb = new StringBuilder();
                if (pipeStdOut != null)
                    command = Command.WithStandardOutputPipe(PipeTarget.Merge(pipeStdOut, PipeTarget.ToStringBuilder(stdOutSb.Value)));
                command = Command.WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutSb.Value));
            }
            else if (pipeStdOut != null)
                command = Command.WithStandardOutputPipe(pipeStdOut);

            try
            {
                if (interruptCts.HasValue)
                {
                    killCts = new CancellationTokenSource();
                    if (graceTimeout.HasValue)
                        interruptCts.Value.Token.Register(() => killCts.Value.CancelAfter(graceTimeout.Value));
                    result = await command.ExecuteAsync(killCts.Value.Token, interruptCts.Value.Token);
                }
                else
                    result = await command.ExecuteAsync();
            }
            catch (TaskCanceledException ex)
            {
                var wasTimedOut = timeoutCts.As(q => q.IsCancellationRequested)
                    .Or(false);
                var wasKilled = killCts.As(q => q.IsCancellationRequested)
                    .Or(false);
                var wasCancelled = cancellationToken?.IsCancellationRequested ?? false;
                var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
                var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(command.Arguments, q)).Or(command.Arguments);
                var maskedErrorMessage = Options.SensitiveDataToMask.As(q => MaskSensitiveData(stdErrSb.ToString(), q)).Or(stdErrSb.ToString());
                var maskedStdOut = new Optional<string>();
                if (stdOutSb.HasValue)
                {
                    if (Options.SensitiveDataToMask.HasValue)
                        maskedStdOut = MaskSensitiveData(stdOutSb.Value.ToString(), Options.SensitiveDataToMask.Value);
                    else
                        maskedStdOut = stdOutSb.Value.ToString();
                }
                throw new CommandExecutionException(new()
                {
                    Command = maskedCommand,
                    Arguments = maskedArguments,
                    Duration = stopwatch.Elapsed,
                    WasTimedOut = wasTimedOut,
                    WasKilled = wasKilled,
                    WasCancelled = wasCancelled,
                    StdErr = maskedErrorMessage,
                    StdOut = maskedStdOut
                }, ex);

            }
            catch (CliWrap.Exceptions.CommandExecutionException ex)
            {
                stopwatch.Stop();
                var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
                var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(command.Arguments, q)).Or(command.Arguments);
                var maskedErrorMessage = Options.SensitiveDataToMask.As(q => MaskSensitiveData(stdErrSb.ToString(), q)).Or(stdErrSb.ToString());
                var maskedStdOut = new Optional<string>();
                if (stdOutSb.HasValue)
                {
                    if (Options.SensitiveDataToMask.HasValue)
                        maskedStdOut = MaskSensitiveData(stdOutSb.Value.ToString(), Options.SensitiveDataToMask.Value);
                    else
                        maskedStdOut = stdOutSb.Value.ToString();
                }
                throw new CommandExecutionException(new()
                {
                    Command = maskedCommand,
                    Arguments = maskedArguments,
                    Duration = stopwatch.Elapsed,
                    WasTimedOut = false,
                    WasKilled = false,
                    WasCancelled = false,
                    ExitCode = ex.ExitCode,
                    StdErr = maskedErrorMessage,
                    StdOut = maskedStdOut
                }, ex);
            }
            finally
            {
                if (timeoutCts.HasValue)
                    timeoutCts.Value.Dispose();
                if (interruptCts.HasValue)
                    interruptCts.Value.Dispose();
                if (killCts.HasValue)
                    killCts.Value.Dispose();
            }

            stopwatch.Stop();
            return new CommandResult
            {
                Arguments = Command.Arguments,
                Command = Options.Command,
                Duration = stopwatch.Elapsed,
                StdOut = stdOutSb.As(sb => sb.ToString()),
                ExitCode = result.ExitCode
            };
        }

    }
}
