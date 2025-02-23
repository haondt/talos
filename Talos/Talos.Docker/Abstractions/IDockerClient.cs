namespace Talos.Docker.Abstractions
{
    public interface IDockerClient
    {
        Task<List<string>> GetCachedContainersAsync(CancellationToken? cancellationToken = null);
        Task<string> GetContainerImageDigestAsync(string container, CancellationToken? cancellationToken = null);
        Task<string> GetContainerImageNameAsync(string container, CancellationToken? cancellationToken = null);
        Task<List<string>> GetContainersAsync(CancellationToken? cancellationToken = null);
        Task<string> GetContainerVersionAsync(string container, CancellationToken? cancellationToken = null);
    }
}