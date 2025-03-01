
using Haondt.Core.Models;

namespace Talos.Domain.Services
{
    public interface IWebHookAuthenticationService
    {
        Task<string> GenerateApiTokenAsync(string name);
        Task<List<string>> ListApiTokensAsync();
        Task RevokeApiToken(string name);
        Task<Result<string>> VerifyApiTokenAsync(string token);
    }
}