using System.Threading.Channels;
using Talos.Api.Models;

namespace Talos.Api.Services
{
    public static class PipelineListenerChannelProvider
    {
        public static Channel<PipelineEventDto> Channel { get; private set; }
        static PipelineListenerChannelProvider()
        {
            Channel = System.Threading.Channels.Channel.CreateUnbounded<PipelineEventDto>(new()
            {
                SingleReader = true,
            });
        }
    }
}
