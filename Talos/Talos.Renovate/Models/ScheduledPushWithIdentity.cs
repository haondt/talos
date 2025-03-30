namespace Talos.Renovate.Models
{
    public readonly record struct ScheduledPushWithIdentity(UpdateIdentity Identity, IScheduledPush Push)
    {

    }
}
