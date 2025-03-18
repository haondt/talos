using Discord;
using Discord.Interactions;
using Talos.Domain.Autocompletion;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("image", "Check which image a container is running")]
        public async Task ImageCommand(
            [
                Summary("host", "Host name"),
                Autocomplete(typeof(HostAutocompleteHandler))
            ] string host,
            [
                Summary("container", "Container name"),
                Autocomplete(typeof(ContainerAutocompleteHandler))
            ] string container)
        {
            await BaseCommand(nameof(ImageCommand),
                (processHandle, o) =>
                {
                    o.CancelButtonId = CreateCancelButtonId(processHandle.Id.ToString());
                    o.Color = Color.Purple;
                },
                socket => socket.StageUpdate(b => b
                    .AddDescriptionPart("**Get container image**")
                    .AddDescriptionPart($"-# {host} » {container}")),
                async (processHandle, socket) =>
                {
                    var availableHosts = dockerClientFactory.GetHosts();

                    if (!availableHosts.Contains(host))
                        throw new ArgumentException($"The specified host '{host}' is not available.");

                    var dockerClient = dockerClientFactory.Connect(host);
                    var imageName = await dockerClient.GetContainerImageNameAsync(container, processHandle.CancellationToken);

                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart($"```\n{imageName}\n```"));
                });
        }
    }
}
