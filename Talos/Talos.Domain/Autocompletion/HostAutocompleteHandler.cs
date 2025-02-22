using Discord;
using Discord.Interactions;
using Talos.Docker.Abstractions;

namespace Talos.Domain.Autocompletion
{
    public class HostAutocompleteHandler(IDockerClientFactory dockerClientFactory) : AutocompleteHandler
    {
        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var input = autocompleteInteraction.Data.Current.Value.ToString() ?? "";
            var hosts = dockerClientFactory.GetHosts();
            var suggestions = hosts
                .Where(h => h.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .Select(h => new AutocompleteResult(h, h))
                .Take(25)
                .ToList();
            return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
        }
    }
}
