using Haondt.Core.Models;
using Talos.Renovate.Abstractions;

namespace Talos.Renovate.Models
{
    public interface IPushToFileWriter
    {
        DetailedResult<IUpdateLocationSnapshot, string> Write(string repositoryDirectory);
    }
    public interface ISubatomicPushToFileWriter : IPushToFileWriter
    {
        string CurrentVersionFriendlyString { get; }
        string NewVersionFriendlyString { get; }
        string CommitMessage { get; }

        bool IsNewerThan(ISubatomicPushToFileWriter other);
        DetailedResult<ISubatomicUpdateLocationSnapshot, string> StageWrite(Func<string, DetailedResult<string, string>> fileReader, Action<string, string> fileWriter);
        IReadOnlyDictionary<string, int> UpdatesPerDomain { get; }
    }

    public interface IScheduledPush
    {
        BumpSize BumpSize { get; }

        IPushToFileWriter Writer { get; }
        string CurrentVersionFriendlyString { get; }
        string NewVersionFriendlyString { get; }
        string CommitMessage { get; }

        bool IsNewerThan(IScheduledPush other);
        IReadOnlyDictionary<string, int> UpdatesPerDomain { get; }
    }


    public interface IUpdateLocationCoordinates
    {
        public UpdateIdentity GetIdentity(string repository);
    }

    public interface IUpdateLocationState
    {
        public TalosSettings Configuration { get; }
        public IUpdateLocationSnapshot Snapshot { get; }

    }



    public interface IUpdateLocationSnapshot
    {
        bool IsEquivalentTo(IUpdateLocationSnapshot locationSnapshot);
    }



    public interface IUpdateLocation
    {
        IUpdateLocationCoordinates Coordinates { get; }
        IUpdateLocationState State { get; }

        Task<Optional<IScheduledPush>> CreateScheduledPushAsync(IImageUpdaterService imageUpdaterService);
    }

    public interface ISubatomicUpdateLocation : IUpdateLocation
    {
        ISubatomicUpdateLocationState SubatomicState { get; }
        ISubatomicPushToFileWriter CreateWriter(ImageUpdateOperation updateOperation);

    }

    public interface ISubatomicUpdateLocationState : IUpdateLocationState
    {
        public ISubatomicUpdateLocationSnapshot SubatomicSnapshot { get; }

    }

    public interface ISubatomicUpdateLocationSnapshot : IUpdateLocationSnapshot
    {
        public ParsedImage CurrentImage { get; }
    }


    public class UpdateOperation
    {

    }


}
