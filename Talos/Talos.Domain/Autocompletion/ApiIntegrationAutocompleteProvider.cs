using Discord;
using Discord.Interactions;

namespace Talos.Domain.Autocompletion
{
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
