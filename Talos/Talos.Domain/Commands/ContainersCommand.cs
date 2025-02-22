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

            await DeferAsync();

            try
            {
                var availableHosts = dockerClientFactory.GetHosts();

                if (!availableHosts.Contains(host))
                    throw new ArgumentException($"The specified host '{host}' is not available.");

                var dockerClient = dockerClientFactory.Connect(host);
                //var containers = await dockerClient.GetContainersAsync();
                // TODO: dummy data
                await Task.Delay(100);
                var containers = new List<string>
                {
                    "foo", "bar", "baz"
                };

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

                await ModifyOriginalResponseAsync(m => m.Embed = embed);
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Command Execution Failed")
                    .WithDescription(ex.Message)
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("Parameters")
                            .WithValue($"`host:{host}`"))
                    .WithColor(Color.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();
                await ModifyOriginalResponseAsync(m => m.Embed = errorEmbed);
                throw;
            }
        }
    }
}
