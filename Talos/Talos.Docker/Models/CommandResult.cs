namespace Talos.Docker.Models
{
    public readonly record struct CommandResult(
        string Command,
        string Arguments,
        TimeSpan Duration)
    {
    }

}
