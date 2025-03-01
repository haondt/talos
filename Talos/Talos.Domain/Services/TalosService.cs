// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Hosting;

namespace Talos.Domain.Services;

public class TalosService : IHostedService
{

    public TalosService()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
