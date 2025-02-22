using CliWrap;
using CliWrap.Builders;
using CliWrap.Exceptions;
using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using Talos.Docker.Models;

namespace Talos.Docker.Services
{
    public record CommandBuilder(
        Command Command,
        CommandOptions Options,
        CommandSettings Settings,
        ILogger<CommandBuilder> Logger)
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

        public async Task<string> ExecuteAndCaptureStdoutAsync(CancellationToken? cancellationToken = null)
        {
            var sb = new StringBuilder();
            var inner = this with
            {
                Command = Command
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(sb))
            };

            await inner.ExecuteAsync(cancellationToken);
            return sb.ToString();
        }

        public async Task<Models.CommandResult> ExecuteAsync(CancellationToken? cancellationToken = null)
        {
            var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
            var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Command.Arguments, q)).Or(Command.Arguments);

            Logger.LogInformation("Executing command: {command} {arguments}",
                maskedCommand, maskedArguments);
            try
            {
                var result = await InternalExecuteAsync(cancellationToken);
                Logger.LogInformation("Completed command: {command} {arguments} in {result}",
                    maskedCommand, maskedArguments, result.Duration);

                return result;
            }
            catch (Exceptions.CommandExecutionException ex)
            {
                if (ex.Result.WasTimedOut)
                {
                    Logger.LogWarning("Command {command} {arguments} timed out after {duration}",
                        maskedCommand, maskedArguments, ex.Result.Duration);
                }
                else if (ex.Result.WasKilled)
                {
                    Logger.LogWarning("Command {command} {arguments} was killed after {duration}",
                        maskedCommand, maskedArguments, ex.Result.Duration);
                }
                else
                {
                    if (ex.Result.ExitCode.HasValue)
                    {
                        Logger.LogError("Command {command} {arguments} failed with exit code {exitCode}.",
                            maskedCommand, maskedArguments, ex.Result.ExitCode.Value);
                    }
                    else
                    {
                        Logger.LogError("Command {command} {arguments} failed with no exit code.",
                            maskedCommand, maskedArguments);
                    }
                }

                throw;
            }
        }

        private async Task<Models.CommandResult> InternalExecuteAsync(CancellationToken? cancellationToken = null)
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

            var result = new Optional<CliWrap.CommandResult>();
            var stdErrSb = new StringBuilder();

            var command = Command.WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrSb));
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
                {
                    result = await command.ExecuteAsync();
                }
            }
            catch (OperationCanceledException ex)
            {
                var wasTimedOut = timeoutCts.As(q => q.IsCancellationRequested)
                    .Or(false);
                var wasKilled = killCts.As(q => q.IsCancellationRequested)
                    .Or(false);
                var wasCancelled = cancellationToken?.IsCancellationRequested ?? false;
                var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
                var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(command.Arguments, q)).Or(command.Arguments);
                var maskedErrorMessage = Options.SensitiveDataToMask.As(q => MaskSensitiveData(stdErrSb.ToString(), q)).Or(stdErrSb.ToString());
                throw new Talos.Docker.Exceptions.CommandExecutionException(new()
                {
                    Command = maskedCommand,
                    Arguments = maskedArguments,
                    Duration = stopwatch.Elapsed,
                    WasTimedOut = wasTimedOut,
                    WasKilled = wasKilled,
                    WasCancelled = wasCancelled,
                    StdErr = maskedErrorMessage
                }, ex);

            }
            catch (CommandExecutionException ex)
            {
                stopwatch.Stop();
                var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
                var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(command.Arguments, q)).Or(command.Arguments);
                var maskedErrorMessage = Options.SensitiveDataToMask.As(q => MaskSensitiveData(stdErrSb.ToString(), q)).Or(stdErrSb.ToString());
                throw new Talos.Docker.Exceptions.CommandExecutionException(new()
                {
                    Command = maskedCommand,
                    Arguments = maskedArguments,
                    Duration = stopwatch.Elapsed,
                    WasTimedOut = false,
                    WasKilled = false,
                    WasCancelled = false,
                    ExitCode = ex.ExitCode,
                    StdErr = maskedErrorMessage
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
            return new Models.CommandResult
            {
                Arguments = Command.Arguments,
                Command = Options.Command,
                Duration = stopwatch.Elapsed
            };
        }

    }
}
