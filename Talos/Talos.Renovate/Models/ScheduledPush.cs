namespace Talos.Renovate.Models
{
    public readonly record struct ScheduledPush(
        ImageUpdateIdentity Target,
        ImageUpdate Update)
    {

    }
}
