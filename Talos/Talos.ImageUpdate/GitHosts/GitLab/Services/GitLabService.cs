using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Web;
using Talos.Core.Abstractions;
using Talos.ImageUpdate.Git.Models;
using Talos.ImageUpdate.GitHosts.GitLab.Models;
using Talos.ImageUpdate.GitHosts.Shared.Models;
using Talos.ImageUpdate.GitHosts.Shared.Services;
using Talos.ImageUpdate.Shared.Constants;

namespace Talos.ImageUpdate.GitHosts.GitLab.Services
{
    public class GitLabService(HttpClient _httpClient,
        ITracer<GitLabService> tracer,
        ILogger<GitLabService> _logger) : IGitHostService
    {
        private const string API_BASE_PATH = "api/v4";
        private const string DEFAULT_BRANCH = "main";

        public HostType Type => HostType.GitLab;

        private static string ExtractGitLabProjectUrlFromRepositoryUrl(string repositoryUrl)
        {
            var uri = new Uri(repositoryUrl);
            var projectPath = uri.AbsolutePath.Trim('/');
            if (projectPath.EndsWith(".git"))
                projectPath = projectPath[..^4].TrimEnd('/');

            return $"{uri.Scheme}://{uri.Host}:{uri.Port}/{API_BASE_PATH}/projects/{Uri.EscapeDataString(projectPath)}";
        }

        public async Task<bool> HasOpenMergeRequestsForBranch(RepositoryConfiguration repository, string sourceBranchName, CancellationToken? cancellationToken = null)
        {
            var projectUrl = ExtractGitLabProjectUrlFromRepositoryUrl(repository.Url);

            var query = HttpUtility.ParseQueryString("");
            query["state"] = "opened";
            query["source_branch"] = sourceBranchName;
            query["per_page"] = "1";
            var uri = new UriBuilder($"{projectUrl}/merge_requests")
            {
                Query = query.ToString()
            };

            HttpResponseMessage result;
            using (var span = tracer.StartSpan(nameof(HasOpenMergeRequestsForBranch)))
            {
                span.SetAttribute("Host", uri.Uri.Host);
                if (cancellationToken.HasValue)
                    result = await _httpClient.GetAsync(uri.Uri, cancellationToken.Value);
                else
                    result = await _httpClient.GetAsync(uri.Uri);
            }
            result.EnsureSuccessStatusCode();

            var mrs = JsonConvert.DeserializeObject<List<GitLabMergeRequestDto>>(await result.Content.ReadAsStringAsync(), SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to deserialize {nameof(GitLabMergeRequestDto)} for projectUrl {projectUrl}");

            _logger.LogInformation("Checked GitLab repository {Repository} for open MRs matching branch name {Branch}. Result: {HasMrs}", uri.Uri.ToString(), sourceBranchName, mrs.Count > 0);

            return mrs.Count > 0;
        }

        public async Task<string> CreateMergeRequestForBranch(RepositoryConfiguration repository, HostConfiguration host, string sourceBranch, string targetBranch, CancellationToken? cancellationToken = null)
        {
            var projectUrl = ExtractGitLabProjectUrlFromRepositoryUrl(repository.Url);

            var mergeRequest = new
            {
                source_branch = sourceBranch,
                target_branch = targetBranch,
                title = $"[Talos] Merge branch '{sourceBranch}' into '{targetBranch}'",
            };

            var content = new StringContent(JsonConvert.SerializeObject(mergeRequest), Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(host.Token))
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", host.Token);

            HttpResponseMessage result;
            using (var span = tracer.StartSpan(nameof(CreateMergeRequestForBranch)))
            {
                span.SetAttribute("Host", new Uri(repository.Url).Host);
                if (cancellationToken.HasValue)
                    result = await _httpClient.PostAsync($"{projectUrl}/merge_requests", content, cancellationToken.Value);
                else
                    result = await _httpClient.PostAsync($"{projectUrl}/merge_requests", content);
            }

            result.EnsureSuccessStatusCode();

            var response = await result.Content.ReadAsStringAsync();
            var mergeRequestDto = JsonConvert.DeserializeObject<GitLabMergeRequestDto>(response, SerializationConstants.SerializerSettings)
                ?? throw new JsonSerializationException($"Failed to deserialize {nameof(GitLabMergeRequestDto)} for projectUrl {projectUrl}");

            _logger.LogInformation("Created merge request for branch {Branch} in repository {Repository}. Merge request IID: {Iid}", sourceBranch, repository.Url, mergeRequestDto.Iid);

            return mergeRequestDto.WebUrl ?? $"Failed to retrieve 'web_url'. Request IID: {mergeRequestDto.Iid}";
        }
    }
}
