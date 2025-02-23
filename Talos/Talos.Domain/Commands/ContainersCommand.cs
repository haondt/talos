using Discord;
using Discord.Interactions;
using System.Text;
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
                o.Title = "List Containers";
                o.Color = Color.Purple;
            });

            socket.StageUpdate(b => b
                .AddStaticField(new()
                {
                    Name = "Parameters",
                    Value = $"```\nhost:{host}\n```"
                }));

            try
            {
                var availableHosts = dockerClientFactory.GetHosts();

                if (!availableHosts.Contains(host))
                    throw new ArgumentException($"The specified host '{host}' is not available.");

                var dockerClient = dockerClientFactory.Connect(host);
                var containers = await dockerClient.GetContainersAsync(processHandle.CancellationToken);

                var containersSb = new StringBuilder();
                if (containers.Count > 0)
                {
                    containersSb.AppendLine("```");
                    foreach (var container in containers)
                        containersSb.AppendLine(container);
                    containersSb.Append("```");
                }
                else
                    containersSb.Append("No containers are currently running on this host.");

                await socket.UpdateAsync(b => b
                    .SetDescription(containersSb.ToString())
                    .AddStaticField(new()
                    {
                        Name = "Host",
                        Value = host,
                        IsInline = true
                    })
                    .AddStaticField(new()
                    {
                        Name = "Count",
                        Value = containers.Count.ToString(),
                        IsInline = true
                    }));

            }
            catch (Exception ex)
            {
                await socket.UpdateAsync(b => b
                    .SetColor(Color.Red)
                    .SetTitle("Command Execution Failed")
                    .SetDescription("> " + string.Join("\n> ", ex.Message.Trim().Split('\n'))));

                throw;
            }
        }
    }
}
