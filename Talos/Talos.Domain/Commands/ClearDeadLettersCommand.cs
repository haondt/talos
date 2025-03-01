using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("cleardeadletters", "Clear the deadletter queue")]
        public async Task ClearDeadLettersCommand()
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Clear deadletters**"));

            try
            {
                var count = await pushQueueMutator.ClearDeadLettersAsync();
                await socket.UpdateAsync(b => b
                    .AddDescriptionPart($"Dropped {count} deadletters"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
