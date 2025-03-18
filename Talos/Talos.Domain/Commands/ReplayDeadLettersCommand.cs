using Discord;
using Discord.Interactions;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("replaydeadletters", "Replay deadletterred pushes")]
        public async Task ReplayDeadLettersCommand()
        {
            await BaseCommand(nameof(ReplayDeadLettersCommand),
                (_, o) =>
                {
                    o.Color = Color.Purple;
                },
                socket => socket
                    .StageUpdate(b => b
                        .AddDescriptionPart("**Replay deadletters**")),
                async (_, socket) =>
                {
                    var count = await pushQueueMutator.ReplayDeadLettersAsync();
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"Replayed {count} deadletters"));
                });
        }
    }
}
