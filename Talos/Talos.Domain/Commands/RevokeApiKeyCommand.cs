using Discord;
using Discord.Interactions;
using Talos.Domain.Models.DiscordEmbedSocket;
using Talos.Domain.Services;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {

        [SlashCommand("revokewebhook", "Revoke an existing webhook")]
        public async Task RevokeWebhookCommand(
            [Summary("name", "Name of the webhook")]
            string name)
        {
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.Color = Color.Green;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Revoke webhook**")
                .AddDescriptionPart($"-# {name}"));

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Name is required");
                name = name.ToLower().Trim();

                await webhookService.RevokeApiToken(name);

                await socket.UpdateAsync(b => b
                    .AddDescriptionPart("Api key removed"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }

    public class ExistingWebhooksAutocompleteHandler(IWebHookAuthenticationService webhookService) : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var names = await webhookService.ListApiTokensAsync();

            return AutocompletionResult.FromSuccess(names
                .Where(i => i.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? "", StringComparison.OrdinalIgnoreCase))
                .Select(q => new AutocompleteResult(q, q))
                .Take(10)
                .ToList());
        }
    }
}
