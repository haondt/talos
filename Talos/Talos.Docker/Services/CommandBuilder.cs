using CliWrap;
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

        [Pure]
        public CommandBuilder WithArguments(string arguments) => this with { Command = Command.WithArguments(arguments) };

        [Pure]
        public CommandBuilder WithTimeout(TimeSpan timeout) => this with { Options = Options with { Timeout = timeout } };

        [Pure]
        public CommandBuilder WithoutTimeout() => this with { Options = Options with { Timeout = Timeout.InfiniteTimeSpan } };

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
            Logger.LogInformation("Executing command: {command} {arguments}",
                Options.Command, Command.Arguments);
            try
            {
                var result = await InternalExecuteAsync();
                Logger.LogInformation("Completed command: {command} {arguments} in {result}",
                    Options.Command, Command.Arguments, result.Duration);

                return result;
            }
            catch (Exceptions.CommandExecutionException ex)
            {
                if (ex.Result.WasTimedOut)
                {
                    Logger.LogWarning("Command {command} {arguments} timed out after {duration}",
                        Options.Command, Command.Arguments, ex.Result.Duration);
                }
                else if (ex.Result.WasKilled)
                {
                    Logger.LogWarning("Command {command} {arguments} was killed after {duration}",
                        Options.Command, Command.Arguments, ex.Result.Duration);
                }
                else
                {
                    Logger.LogError("Command {command} {arguments} failed with exit code {exitCode}.",
                        Options.Command, Command.Arguments, ex.Result.ExitCode);
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

            var wasTimedOut = false;
            var wasKilled = false;
            var result = new Optional<CliWrap.CommandResult>();
            var stdErrSb = new StringBuilder();

            var command = Command.WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrSb));
            try
            {
                result = cts.HasValue
                   ? await command.ExecuteAsync(cts.Value.KillCts.Token, cts.Value.InterruptCts.Token)
                   : await command.ExecuteAsync();
            }
            catch (OperationCanceledException)
            {
                wasTimedOut = cts.As(q => q.InterruptCts.IsCancellationRequested)
                    .Or(false);
                wasKilled = cts.As(q => q.KillCts.IsCancellationRequested)
                    .Or(false);
            }
            catch (CommandExecutionException ex)
            {
                stopwatch.Stop();
                throw new Talos.Docker.Exceptions.CommandExecutionException(new()
                {
                    Command = Options.Command,
                    Arguments = command.Arguments,
                    Duration = stopwatch.Elapsed,
                    WasTimedOut = wasTimedOut,
                    WasKilled = wasKilled,
                    ExitCode = ex.ExitCode,
                    StdErr = stdErrSb.ToString()
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
