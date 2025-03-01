using Discord;
using Discord.Interactions;
using Talos.Domain.Autocompletion;
using Talos.Domain.Models.DiscordEmbedSocket;

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
            using var processHandle = processRegistry.RegisterProcess();
            var cancellationId = $"cancel-process-{processHandle.Id}";
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.CancelButtonId = cancellationId;
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**Get container image**")
                .AddDescriptionPart($"-# {host} » {container}"));

            try
            {
                var availableHosts = dockerClientFactory.GetHosts();

                if (!availableHosts.Contains(host))
                    throw new ArgumentException($"The specified host '{host}' is not available.");

                var dockerClient = dockerClientFactory.Connect(host);
                var imageName = await dockerClient.GetContainerImageNameAsync(container, processHandle.CancellationToken);

                await socket.UpdateAsync(b => b
                    .AddDescriptionPart($"```\n{imageName}\n```"));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
