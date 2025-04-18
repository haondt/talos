using Talos.ImageUpdate.Skopeo.Models;
using Talos.ImageUpdate.Skopeo.Services;

namespace Talos.ImageUpdate.Tests.Fakes
{
    internal class FakeSkopeoService(Dictionary<string, List<string>> tagsByName, Dictionary<string, string> digestsByNameAndTag) : ISkopeoService
    {
        public Task<SkopeoInspectResponse> Inspect(string image, CancellationToken? cancellationToken = null)
        {
            return Task.FromResult(new SkopeoInspectResponse
            {
                Architecture = "",
                Created = DateTime.UtcNow,
                Digest = digestsByNameAndTag[image],
                DockerVersion = "",
                Name = "",
                Os = ""
            });
        }

        public Task<List<string>> ListTags(string image, CancellationToken? cancellationToken = null)
        {
            return Task.FromResult(tagsByName[image]);
        }
    }
}