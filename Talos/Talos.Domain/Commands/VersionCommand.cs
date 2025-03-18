using Discord;
using Discord.Interactions;
using Talos.Domain.Autocompletion;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("version", "Check container image version via 'org.opencontainers.image.version' label")]
        public async Task VersionCommand(
            [
                Summary("host", "Host name"),
                Autocomplete(typeof(HostAutocompleteHandler))
            ] string host,
            [
                Summary("container", "Container name"),
                Autocomplete(typeof(ContainerAutocompleteHandler))
            ] string container)
        {
            await BaseCommand(nameof(VersionCommand),
                (processHandle, o) =>
                {
                    o.CancelButtonId = CreateCancelButtonId(processHandle.Id.ToString());
                    o.Color = Color.Purple;
                },
                socket => socket
                    .StageUpdate(b => b
                    .AddDescriptionPart("**Get container image version**")
                    .AddDescriptionPart($"-# {host} » {container}")),
                async (processHandle, socket) =>
                {
                    var availableHosts = dockerClientFactory.GetHosts();

                    if (!availableHosts.Contains(host))
                        throw new ArgumentException($"The specified host '{host}' is not available.");

                    var dockerClient = dockerClientFactory.Connect(host);
                    var version = await dockerClient.GetContainerVersionAsync(container, processHandle.CancellationToken);

                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"```{version}```"));
                });
        }
    }
}
