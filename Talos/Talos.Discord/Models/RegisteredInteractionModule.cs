using Talos.Discord.Abstractions;

namespace Talos.Discord.Models
{
    public class RegisteredInteractionModule : IRegisteredInteractionModule
    {
        public required Type Type { get; set; }
    }
}
