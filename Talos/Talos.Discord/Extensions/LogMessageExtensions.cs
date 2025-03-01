using Discord;
using Microsoft.Extensions.Logging;

namespace Talos.Discord.Extensions
{
    public static class LogMessageExtensions
    {
        public static void LogTo<T>(this LogMessage logMessage, ILogger<T> logger)
        {
            var logLevel = logMessage.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Trace,
                _ => LogLevel.Information
            };

            if (logMessage.Exception != null)
            {
                if (!string.IsNullOrEmpty(logMessage.Message))
                    logger.Log(
                        logLevel,
                        logMessage.Exception,
                        "{Source} {Message}",
                        logMessage.Source,
                        logMessage.Message);
                else
                    logger.Log(
                        logLevel,
                        logMessage.Exception,
                        "{Source}",
                        logMessage.Source);
            }
            else
            {

                if (!string.IsNullOrEmpty(logMessage.Message))
                    logger.Log(
                        logLevel,
                        "{Source} {Message}",
                        logMessage.Source,
                        logMessage.Message);
                else
                    logger.Log(
                        logLevel,
                        "{Source}",
                        logMessage.Source);

            }
        }
    }
}
