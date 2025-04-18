using Haondt.Core.Models;
using Talos.ImageUpdate.ImageParsing.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.ImageUpdating.Services
{
    public interface IImageUpdaterService
    {
        Task CompletePushAsync(ScheduledPushWithIdentity push, IUpdateLocationSnapshot snapshot);
        Task<(string Digest, AbsoluteDateTime Created)> GetDigestAsync(ParsedImage image, ParsedTag tag);
        Task<List<ParsedTag>> GetSortedCandidateTagsAsync(ParsedImage parsedActiveImage, BumpSize maxBumpSize);
        Task<Optional<ScheduledPushWithIdentity>> HandleImageUpdateAsync(UpdateIdentity id, IUpdateLocation location);
        Optional<BumpSize> IsUpgrade(Optional<ParsedTagAndDigest> from, ParsedTag toTag, string toDigest);
    }
}