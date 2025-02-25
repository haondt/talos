using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IGitHostServiceProvider
    {
        IGitHostService GetGitHost(HostConfiguration host);
    }
}