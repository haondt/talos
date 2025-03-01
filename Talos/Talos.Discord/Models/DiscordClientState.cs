namespace Talos.Discord.Models
{
    public class DiscordClientState
    {
        private readonly CancellationTokenSource _shutdownCts = new();
        private readonly TaskCompletionSource _startTcs = new();
        public CancellationToken CancellationToken => _shutdownCts.Token;
        public Task StartTask => _startTcs.Task;

        public void SignalStart()
        {
            _startTcs.TrySetResult();
        }

        public Task SignalShutdown()
        {
            _startTcs.TrySetResult();
            return _shutdownCts.CancelAsync();
        }
    }
}
