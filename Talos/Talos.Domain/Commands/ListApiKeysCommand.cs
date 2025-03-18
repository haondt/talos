using Discord;
using Discord.Interactions;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {

        [SlashCommand("listwebhooks", "List existing webhooks")]
        public async Task ListWebhooksCommand()
        {
            await BaseCommand(nameof(ListWebhooksCommand),
                (_, o) =>
                {
                    o.Color = Color.Green;
                },
                socket => socket
                    .StageUpdate(b => b
                    .AddDescriptionPart("**List webhooks**")),
                async (_, socket) =>
                {
                    var names = await webhookService.ListApiTokensAsync();
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"```\n{string.Join('\n', names)}\n```"));
                });
        }
    }
}
