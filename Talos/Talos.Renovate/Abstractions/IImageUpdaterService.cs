
using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IImageUpdaterService
    {
        Task RunUpdateAsync(CancellationToken? cancellationToken = null);
        (HostConfiguration Host, RepositoryConfiguration Repository) GetRepositoryConfiguration(string remoteUrl);
        Task<(List<ScheduledPushWithIdentity> SuccessfulPushes, List<ScheduledPushDeadLetter> FailedPushes)> PushUpdates(HostConfiguration host, RepositoryConfiguration repositoryConfiguration, List<ScheduledPushWithIdentity> scheduledPushes, CancellationToken? cancellationToken = null);
        Task<bool> CheckIfCommitBelongsToUs(string commit);
        Task<List<ParsedTag>> GetSortedCandidateTagsAsync(ParsedImage parsedActiveImage, BumpSize maxBumpSize, bool insertDefaultDomain = true);
        Task<(string Digest, AbsoluteDateTime Created)> GetDigestAsync(ParsedImage image, ParsedTag tag);
        Optional<BumpSize> IsUpgrade(Optional<ParsedTagAndDigest> from, ParsedTag toTag, string toDigest);
    }
}