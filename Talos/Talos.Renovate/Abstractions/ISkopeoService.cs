

using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface ISkopeoService
    {
        Task<SkopeoInspectResponse> Inspect(string image, CancellationToken? cancellationToken = null);
        Task<List<string>> ListTags(string image, CancellationToken? cancellationToken = null);
    }
}
