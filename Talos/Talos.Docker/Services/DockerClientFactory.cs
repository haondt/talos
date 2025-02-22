using Haondt.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Talos.Docker.Abstractions;
using Talos.Docker.Models;

namespace Talos.Docker.Services
{
    public class DockerClientFactory(IOptions<DockerSettings> options,
        IServiceProvider serviceProvider) : IDockerClientFactory
    {
        public IDockerClient Connect(string host)
        {
            var settings = options.Value.Hosts[host];
            return ActivatorUtilities.CreateInstance<DockerClient>(serviceProvider, new DockerClientOptions
            {
                Host = settings.Host == DockerConstants.LOCALHOST
                    ? new Optional<string>() : new(settings.Host),
                DockerVersion = settings.DockerVersion,
                ForceRecreateOnUp = settings.ForceRecreateOnUp
            });
        }

        public List<string> GetHosts()
        {
            return options.Value.Hosts.Keys.ToList();
        }
    }
}
