using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Talos.Core.Abstractions;
using Talos.Docker.Abstractions;

namespace Talos.Domain.Autocompletion
{
    public class HostAutocompleteHandler(
        IDockerClientFactory dockerClientFactory,
        ITracer<ApiIntegrationAutocompleteProvider> tracer,
        ILogger<ApiIntegrationAutocompleteProvider> logger
        ) : AutocompleteHandler
    {
        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            using var span = tracer.StartSpan(nameof(GenerateSuggestionsAsync), SpanKind.Server);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
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
