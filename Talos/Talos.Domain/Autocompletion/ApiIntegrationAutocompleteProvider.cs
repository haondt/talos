using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Talos.Core.Abstractions;

namespace Talos.Domain.Autocompletion
{
    public class ApiIntegrationAutocompleteProvider(
        ITracer<ApiIntegrationAutocompleteProvider> tracer,
        ILogger<ApiIntegrationAutocompleteProvider> logger
        ) : AutocompleteHandler
    {
        public static readonly List<string> Integrations = new()
        {
            "gitlab"
        };

        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            using var span = tracer.StartSpan(nameof(GenerateSuggestionsAsync), SpanKind.Server);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
            return Task.FromResult(AutocompletionResult.FromSuccess(Integrations
                .Where(i => i.Contains(autocompleteInteraction.Data.Current.Value.ToString() ?? ""))
                .Select(q => new AutocompleteResult(q, q))
                .Take(10)
                .ToList()));
        }
    }
}
