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

        public async Task<string> ExecuteAndCaptureStdoutAsync()
        {
            var sb = new StringBuilder();
            var inner = this with
            {
                Command = Command
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(sb))
            };

            await inner.ExecuteAsync();
            return sb.ToString();
        }

        public async Task<Models.CommandResult> ExecuteAsync()
        {
            var maskedCommand = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Options.Command, q)).Or(Options.Command);
            var maskedArguments = Options.SensitiveDataToMask.As(q => MaskSensitiveData(Command.Arguments, q)).Or(Command.Arguments);

            Logger.LogInformation("Executing command: {command} {arguments}",
                maskedCommand, maskedArguments);
            try
            {
                var result = await InternalExecuteAsync();
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

        private async Task<Models.CommandResult> InternalExecuteAsync()
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

            var cts = new Optional<(CancellationTokenSource InterruptCts, CancellationTokenSource KillCts)>();
            if (timeout.HasValue)
            {
                var interruptCts = new CancellationTokenSource(timeout.Value);
                CancellationTokenSource killCts;
                if (graceTimeout.HasValue)
                    killCts = new CancellationTokenSource(timeout.Value + graceTimeout.Value);
                else
                {
                    killCts = new CancellationTokenSource(Timeout.Infinite);
                }

                cts = (interruptCts, killCts);
            }

            var result = new Optional<CliWrap.CommandResult>();
            var stdErrSb = new StringBuilder();

            var command = Command.WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrSb));
            try
            {
                result = cts.HasValue
                   ? await command.ExecuteAsync(cts.Value.KillCts.Token, cts.Value.InterruptCts.Token)
                   : await command.ExecuteAsync();
            }
            catch (OperationCanceledException ex)
            {
                var wasTimedOut = cts.As(q => q.InterruptCts.IsCancellationRequested)
                    .Or(false);
                var wasKilled = cts.As(q => q.KillCts.IsCancellationRequested)
                    .Or(false);
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
                    ExitCode = ex.ExitCode,
                    StdErr = maskedErrorMessage
                }, ex);
            }
            finally
            {
                if (cts.HasValue)
                {
                    cts.Value.InterruptCts.Dispose();
                    cts.Value.KillCts.Dispose();
                }
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
