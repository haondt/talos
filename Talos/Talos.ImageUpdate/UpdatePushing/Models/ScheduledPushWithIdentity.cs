namespace Talos.ImageUpdate.UpdatePushing.Models
{
    public readonly record struct ScheduledPushWithIdentity(UpdateIdentity Identity, IScheduledPush Push);
}
