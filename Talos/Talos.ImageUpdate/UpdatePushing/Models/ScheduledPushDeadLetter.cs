using Haondt.Core.Models;

namespace Talos.ImageUpdate.UpdatePushing.Models
{
    public readonly record struct ScheduledPushDeadLetter(
        ScheduledPushWithIdentity Push,
        string Reason,
        Optional<string> ExceptionStackTrace = default)
    {
    }

}
