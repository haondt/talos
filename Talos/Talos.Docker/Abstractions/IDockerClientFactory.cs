
namespace Talos.Docker.Abstractions
{
    public interface IDockerClientFactory
    {
        IDockerClient Connect(string host);
        List<string> GetHosts();
    }
}