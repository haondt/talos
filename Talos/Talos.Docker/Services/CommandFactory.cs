using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Docker.Abstractions;
using Talos.Docker.Models;

namespace Talos.Docker.Services
{
    public class CommandFactory(
        IOptions<CommandSettings> options,
        ILogger<CommandBuilder> builderLogger) : ICommandFactory
    {
        public CommandBuilder Create(string command)
        {
            return CommandBuilder.Wrap(
                command,
                options.Value,
                builderLogger);
        }
    }
}
