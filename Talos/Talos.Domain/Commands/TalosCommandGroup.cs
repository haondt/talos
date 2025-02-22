using Discord.Interactions;
using Talos.Docker.Abstractions;
using Talos.Domain.Abstractions;

namespace Talos.Domain.Commands
{
    [Group("t", "Talos")]
    public partial class TalosCommandGroup(
        IDockerClientFactory dockerClientFactory,
        IDiscordCommandProcessRegistry processRegistry)
        : InteractionModuleBase<SocketInteractionContext>
    {

        [ComponentInteraction("cancel-process-*", ignoreGroupNames: true)]
        public Task CancelProcessAsync(string cancellationId)
        {
            if (Guid.TryParse(cancellationId, out var commandId))
                processRegistry.CancelProcess(commandId);

            return Task.CompletedTask;
        }
    }
}
