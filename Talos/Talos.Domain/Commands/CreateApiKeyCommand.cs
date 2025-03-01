using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;

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
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Green;
                o.Ephemeral = true;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Create webhook**")
                .AddDescriptionPart($"-# ({integration}) {name}"));

            try
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
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }

    public class ApiIntegrationAutocompleteProvider : AutocompleteHandler
    {
        public static readonly List<string> Integrations = new()
        {
            "gitlab"
        };

        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            return Task.FromResult(AutocompletionResult.FromSuccess(Integrations
                .Where(i => i.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? ""))
                .Select(q => new AutocompleteResult(q, q))
                .Take(10)
                .ToList()));
        }
    }
}
