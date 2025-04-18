using Talos.ImageUpdate.Git.Models;

namespace Talos.ImageUpdate.GitHosts.Shared.Services
{
    public interface IGitHostServiceProvider
    {
        IGitHostService GetGitHost(HostConfiguration host);
    }
}