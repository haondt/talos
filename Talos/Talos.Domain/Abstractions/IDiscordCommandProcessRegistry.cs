

using Haondt.Core.Models;

namespace Talos.Domain.Abstractions
{
    public interface IDiscordCommandProcessRegistry
    {
        void CancelProcess(Guid id);
        void CompleteProcess(Guid id);
        IDiscordCommandProcessHandle RegisterProcess();
        Optional<IDiscordCommandProcessHandle> TryGetProcess(Guid processId);
    }
}