using Haondt.Core.Models;

namespace Talos.Renovate.Models
{
    public readonly record struct ScheduledPushDeadLetter(
        ScheduledPushWithIdentity Push,
        string Reason,
        Optional<string> ExceptionStackTrace = default)
    {
    }

}
