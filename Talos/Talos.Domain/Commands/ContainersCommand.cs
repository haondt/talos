using Discord;
using Discord.Interactions;
using Talos.Domain.Autocompletion;
using Talos.Domain.Models.DiscordEmbedSocket;

namespace Talos.Domain.Commands
{
    public partial class TalosCommandGroup
    {
        [SlashCommand("containers", "List the containers running on the host")]
        public async Task ContainersCommand(
            [
                Summary("host", "Host name"),
                Autocomplete(typeof(HostAutocompleteHandler))
            ] string host)
        {
            using var processHandle = processRegistry.RegisterProcess();
            var cancellationId = $"cancel-process-{processHandle.Id}";
            await using var socket = await DiscordEmbedSocket.OpenSocketAsync(this, o =>
            {
                o.CancelButtonId = cancellationId;
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddDescriptionPart("**List containers**")
                .AddDescriptionPart($"-# {host}"));

            try
            {
                var availableHosts = dockerClientFactory.GetHosts();

                if (!availableHosts.Contains(host))
                    throw new ArgumentException($"The specified host '{host}' is not available.");

                var dockerClient = dockerClientFactory.Connect(host);
                var containers = await dockerClient.GetContainersAsync(processHandle.CancellationToken);

                if (containers.Count > 0)
                {
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart("```\n" + string.Join(' ', containers) + "\n```")
                        .AddDescriptionPart($"Total: {containers.Count}"));
                }
                else
                    await socket.UpdateAsync(b => b
                        .AddDescriptionPart("No containers are currently running on this host."));
            }
            catch (Exception ex)
            {
                await RenderErrorAsync(socket, ex);
                throw;
            }
        }
    }
}
