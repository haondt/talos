using Discord;
using Discord.Interactions;
using System.Text;
using Talos.Domain.Autocompletion;

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
            var title = "List Containers";
            var color = Color.Purple;

            using var processHandle = processRegistry.RegisterProcess();

            var initialEmbed = new EmbedBuilder()
                .WithTitle("Working on it...")
                .WithColor(Color.Parse("#e8ce25"))
                .Build();
            var button = new ButtonBuilder()
                .WithCustomId($"cancel-process-{processHandle.Id}")
                .WithLabel("Cancel")
                .WithStyle(ButtonStyle.Danger);
            var component = new ComponentBuilder()
                .WithButton(button)
                .Build();

            await RespondAsync(embed: initialEmbed, components: component);

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


                var embedBuilder = new EmbedBuilder()
                    .WithTitle(title)
                    .WithDescription(containersSb.ToString())
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("Host")
                            .WithValue(host)
                            .WithIsInline(true),
                        new EmbedFieldBuilder()
                            .WithName("Count")
                            .WithValue(containers.Count)
                            .WithIsInline(true))
                    .WithColor(color)
                    .WithTimestamp(DateTimeOffset.UtcNow);
                var embed = embedBuilder.Build();

                await ModifyOriginalResponseAsync(m => { m.Embed = embed; m.Components = new ComponentBuilder().Build(); });
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Command Execution Failed")
                    .WithDescription("> " + string.Join("\n> ", ex.Message.Trim().Split('\n')))
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("Parameters")
                            .WithValue($"`host:{host}`"))
                    .WithColor(Color.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();
                await ModifyOriginalResponseAsync(m => { m.Embed = errorEmbed; m.Components = new ComponentBuilder().Build(); });
                throw;
            }
        }
    }
}
