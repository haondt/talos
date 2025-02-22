using Haondt.Core.Models;
using System.Collections.Concurrent;
using Talos.Domain.Abstractions;
using Talos.Domain.Models;

namespace Talos.Domain.Services
{
    public class DiscordCommandProcessRegistry : IDiscordCommandProcessRegistry
    {
        private readonly ConcurrentDictionary<Guid, DiscordCommandProcesss> _processes = new();

        public IDiscordCommandProcessHandle RegisterProcess()
        {
            var process = new DiscordCommandProcesss
            {
                Id = Guid.NewGuid(),
                CancellationTokenSource = new CancellationTokenSource()
            };

            if (!_processes.TryAdd(process.Id, process))
                throw new InvalidOperationException($"Unable to register a new process");

            return CreateHandle(process);
        }

        private DiscordCommandProcessHandle CreateHandle(DiscordCommandProcesss process)
            => new(() => CompleteProcess(process.Id))
            {
                Id = process.Id,
                CancellationToken = process.CancellationTokenSource.Token
            };


        public void CancelProcess(Guid id)
        {
            if (!_processes.TryRemove(id, out var process))
                return;
            process.CancellationTokenSource.Cancel();
            process.CancellationTokenSource.Dispose();
        }

        public void CompleteProcess(Guid id)
        {
            if (!_processes.TryRemove(id, out var process))
                return;

            process.CancellationTokenSource.Dispose();
        }

        public Optional<IDiscordCommandProcessHandle> TryGetProcess(Guid processId)
        {
            if (_processes.TryGetValue(processId, out var process))
                return CreateHandle(process);
            return new();
        }
    }
}
