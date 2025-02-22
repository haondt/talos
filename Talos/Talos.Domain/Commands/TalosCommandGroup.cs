using Discord.Interactions;
using Talos.Docker.Abstractions;

namespace Talos.Domain.Commands
{
    [Group("t", "Talos")]
    public partial class TalosCommandGroup(IDockerClientFactory dockerClientFactory)
        : InteractionModuleBase<SocketInteractionContext>
    {

    }
}
