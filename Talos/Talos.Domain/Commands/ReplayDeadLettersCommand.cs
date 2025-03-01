using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("replaydeadletters", "Replay deadletterred pushes")]
        public async Task ReplayDeadLettersCommand()
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Replay deadletters**"));

            try
            {
                var count = await pushQueueMutator.ReplayDeadLettersAsync();
                await socket.UpdateAsync(b => b
                    .AddDescriptionPart($"Replayed {count} deadletters"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
