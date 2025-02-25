namespace Talos.Integration.Command.Models
{
    public class CommandSettings
    {
        public int DefaultTimeoutSeconds { get; set; } = 300;
        public int DefaultGracePeriodSeconds { get; set; } = 60;
    }
}