using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {

        [SlashCommand("listwebhooks", "List existing webhooks")]
        public async Task ListWebhooksCommand()
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Green;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**List webhooks**"));

            try
            {
                var names = await webhookService.ListApiTokensAsync();
                await socket.UpdateAsync(b => b
                    .AddDescriptionPart($"```\n{string.Join('\n', names)}\n```"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
