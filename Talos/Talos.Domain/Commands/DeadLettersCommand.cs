using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("deadletters", "Get the size of the deadletter queue for update pushes")]
        public async Task DeadLettersCommand()
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Deadletters**"));

            try
            {
                var count = await pushQueueMutator.GetDeadLetterQueueSizeAsync();
                await socket.UpdateAsync(b => b
                    .AddDescriptionPart($"There are {count} pushes in the deadletter queue"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
