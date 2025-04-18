using Talos.ImageUpdate.Skopeo.Models;

namespace Talos.ImageUpdate.Skopeo.Services
{
    public interface ISkopeoService
    {
        Task<SkopeoInspectResponse> Inspect(string image, CancellationToken? cancellationToken = null);
        Task<List<string>> ListTags(string image, CancellationToken? cancellationToken = null);
    }
}
