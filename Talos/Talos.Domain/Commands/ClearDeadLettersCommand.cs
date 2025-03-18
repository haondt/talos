using Discord;
using Discord.Interactions;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("cleardeadletters", "Clear the deadletter queue")]
        public async Task ClearDeadLettersCommand()
        {
            await BaseCommand(nameof(ClearDeadLettersCommand),
                (_, o) => o.Color = Color.Purple,
                socket => socket
                    .StageUpdate(b => b
                    .AddDescriptionPart("**Clear deadletters**")),
                async (_, socket) =>
                {
                    var count = await pushQueueMutator.ClearDeadLettersAsync();
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"Dropped {count} deadletters"));
                });
        }
    }
}
