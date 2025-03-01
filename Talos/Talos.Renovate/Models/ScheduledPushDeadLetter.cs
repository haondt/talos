using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public readonly record struct ScheduledPushDeadLetter(
        ScheduledPush Push,
        string Reason,
        Optional<string> ExceptionStackTrace = default)
    {
    }

}
