using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Talos.Core.Abstractions;
using Talos.Docker.Abstractions;

namespace Talos.Domain.Autocompletion
{
    public class ContainerAutocompleteHandler(IDockerClientFactory dockerClientFactory,
        ITracer<ContainerAutocompleteHandler> tracer,
        ILogger<ContainerAutocompleteHandler> logger
        ) : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            using var span = tracer.StartSpan(nameof(GenerateSuggestionsAsync), SpanKind.Server);
            using var _ = logger.BeginScope(new Dictionary<string, object> { { "TraceId", span.TraceId } });
            var hostOption = autocompleteInteraction.Data.Options.FirstOrDefault(o => o.Name == "host");
            var host = hostOption?.Value?.ToString()?.ToLowerInvariant() ?? "";
            var input = autocompleteInteraction.Data.Current.Value.ToString() ?? "";

            var hosts = dockerClientFactory.GetHosts();
            if (!hosts.Contains(host))
                return AutocompletionResult.FromSuccess([]);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var client = dockerClientFactory.Connect(host);
            var containers = await client.GetCachedContainersAsync(cts.Token);

            var suggestions = containers
                .Where(h => h.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .Select(h => new AutocompleteResult(h, h))
                .Take(25)
                .ToList();
            return AutocompletionResult.FromSuccess(suggestions);
        }
    }
}
