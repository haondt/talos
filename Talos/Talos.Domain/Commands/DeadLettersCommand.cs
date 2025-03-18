using Discord;
using Discord.Interactions;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("deadletters", "Get the size of the deadletter queue for update pushes")]
        public async Task DeadLettersCommand()
        {
            await BaseCommand(nameof(DeadLettersCommand),
                (_, o) => o.Color = Color.Purple,
                socket => socket.StageUpdate(b => b
                    .AddDescriptionPart("**Deadletters**")),
                async (_, socket) =>
                {
                    var count = await pushQueueMutator.GetDeadLetterQueueSizeAsync();
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"There are {count} pushes in the deadletter queue"));
                });
        }
    }
}
