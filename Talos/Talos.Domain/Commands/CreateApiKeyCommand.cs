using Discord;
using Discord.Interactions;
using Talos.Domain.Autocompletion;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        private string CreateWebhookUrl(string integration)
        {
            return $"{apiSettings.Value.BaseUrl.TrimEnd('/').Trim()}/webhooks/{integration}";
        }


        [SlashCommand("createwebhook", "Create a new webhook")]
        public async Task CreateWebhookCommand(
            [Summary("name", "Name for the token. Must be unique")]
            string name,
            [
                Summary("integration", "Integration name"),
                Autocomplete(typeof(ApiIntegrationAutocompleteProvider))
            ] string integration)
        {
            await BaseCommand(nameof(CreateWebhookCommand),
                (_, o) =>
                {
                    o.Color = Color.Green;
                    o.Ephemeral = true;
                },
                socket => socket
                    .StageUpdate(b => b
                    .AddDescriptionPart("**Create webhook**")
                    .AddDescriptionPart($"-# ({integration}) {name}")),
                async (_, socket) =>
                {

                    if (string.IsNullOrWhiteSpace(name))
                        throw new ArgumentException("Name is required");
                    name = name.ToLower().Trim();

                    if (!ApiIntegrationAutocompleteProvider.Integrations.Contains(integration))
                        throw new ArgumentException($"Unrecognized integration '{integration}'");

                    var token = await webhookService.GenerateApiTokenAsync(name);
                    string webhookUrl = CreateWebhookUrl(integration);

                    await socket.UpdateAsync(b => b
                        .AddStaticField(new()
                        {
                            Name = "Url",
                            Value = $"```\n{webhookUrl}\n```"
                        })
                        .AddStaticField(new()
                        {
                            Name = "Bearer Token",
                            Value = $"```\n{token}\n```"
                        }));
                });
        }
    }
}
