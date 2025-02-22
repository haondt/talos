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
                HostOptions = DetermineHostOptions(settings),
                DockerVersion = settings.DockerVersion,
                ForceRecreateOnUp = settings.ForceRecreateOnUp
            });
        }

        private static DockerHostOptions DetermineHostOptions(DockerHostSettings hostSettings)
        {
            if (hostSettings.SSHConfig != null)
            {
                if (hostSettings.SSHConfig.IdentityFile != null)
                    return new SSHIdentityFileDockerHostOptions
                    {
                        User = hostSettings.SSHConfig.User,
                        Host = hostSettings.SSHConfig.Host,
                        IdentityFile = hostSettings.SSHConfig.IdentityFile
                    };
            }

            return new LocalDockerHostOptions();
        }

        public List<string> GetHosts()
        {
            return options.Value.Hosts.Keys.ToList();
        }
    }
}
