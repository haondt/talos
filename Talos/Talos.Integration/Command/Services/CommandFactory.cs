using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talos.Core.Abstractions;
using Talos.Integration.Command.Abstractions;
using Talos.Integration.Command.Models;

namespace Talos.Integration.Command.Services
{
    public class CommandFactory(
        IOptions<CommandSettings> options,
        ITracer<CommandBuilder> builderTracer,
        ILogger<CommandBuilder> builderLogger) : ICommandFactory
    {
        public CommandBuilder Create(string command)
        {
            return CommandBuilder.Wrap(
                command,
                options.Value,
                builderTracer,
                builderLogger);
        }
    }
}
