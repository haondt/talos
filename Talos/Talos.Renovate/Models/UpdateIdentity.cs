using Haondt.Core.Extensions;
using Haondt.Core.Models;
using Talos.Core.Models;

namespace Talos.Renovate.Models
{
    public readonly record struct UpdateIdentity
    {
        public required string GitRemoteUrl { get; init; }
        public required Optional<string> GitBranch { get; init; }
        public required UpdateType Type { get; init; }
        public required string Hash { get; init; }
        public required string ShortFriendlyHashData { get; init; }

        public static UpdateIdentity Atomic(string gitRemoteUrl, Optional<string> gitBranch, IEnumerable<UpdateIdentity> components)
        {
            var sorted = components.OrderBy(q => q.Type)
                .ThenBy(q => q.Hash);
            var hashInput = string.Join(";", sorted.Select(q => $"{q.Type}:{q.Hash}"));
            var hash = HashUtils.ComputeSha256HashHexString(hashInput);

            return new()
            {
                GitRemoteUrl = gitRemoteUrl,
                GitBranch = gitBranch,
                Type = UpdateType.Atomic,
                Hash = hash,
                ShortFriendlyHashData = string.Join(',', components.Select(q => q.ShortFriendlyHashData))
            };
        }
        public static UpdateIdentity DockerCompose(string gitRemoteUrl, Optional<string> gitBranch, string relativeFilePath, string serviceKey)
        {
            var hashInput = $"{relativeFilePath}:{serviceKey}";
            var hash = HashUtils.ComputeSha256HashHexString(hashInput);

            return new()
            {
                GitRemoteUrl = gitRemoteUrl,
                GitBranch = gitBranch,
                Type = UpdateType.DockerCompose,
                Hash = hash,
                ShortFriendlyHashData = serviceKey
            };
        }
        public static UpdateIdentity Dockerfile(string gitRemoteUrl, Optional<string> gitBranch, string relativeFilePath, int line)
        {
            var hashInput = $"{relativeFilePath}:{line}";
            var hash = HashUtils.ComputeSha256HashHexString(hashInput);

            return new()
            {
                GitRemoteUrl = gitRemoteUrl,
                GitBranch = gitBranch,
                Type = UpdateType.Dockerfile,
                Hash = hash,
                ShortFriendlyHashData = relativeFilePath.Trim('/')
            };
        }

        public override string ToString()
        {
            var branchInfix = GitBranch.As(q => $"[{q}]").Or("");
            return $"{Type}/{GitRemoteUrl}{branchInfix}/{Hash}".Replace(":", "");
        }
    }
}
