using Discord.Interactions;
using Talos.Docker.Abstractions;

namespace Talos.Domain.Commands
{
    public class ContainersCommand(IDockerClientFactory dockerClientFactory) : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("containers", "List the containers running on the host")]
        public async Task ExecuteAsync([Summary("host", "Host name")] string host)
        {
            await DeferAsync();
            await Task.Delay(1000);
            await ModifyOriginalResponseAsync(m => m.Content = "this i my response");
            await Task.Delay(5000);
            await ModifyOriginalResponseAsync(m => m.Content = "this is my new response");
            await Task.Delay(5000);
            await ModifyOriginalResponseAsync(m => m.Content = "this is my even newer response");
            await Task.Delay(5000);
            await FollowupAsync($"you said host: {host}");
        }
    }
}
