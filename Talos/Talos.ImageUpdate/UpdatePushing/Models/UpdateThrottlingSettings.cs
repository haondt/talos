namespace Talos.ImageUpdate.UpdatePushing.Models
{
    public class UpdateThrottlingSettings
    {
        public int QueuePollingFrequencyInSeconds { get; set; } = 60;
        public Dictionary<string, ThrottlingConfiguration> Domains { get; set; } = [];
    }

    public class ThrottlingConfiguration
    {
        public int Limit { get; set; } = 0;
        public TimeUnit Per { get; set; } = TimeUnit.Hour;
    }

    public enum TimeUnit
    {
        Second = 1,
        Minute = 60,
        Hour = 3600,
        Day = 86400,
        Week = 604800,
        Month = 18446400
    }
}