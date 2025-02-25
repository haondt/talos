using Talos.Integration.Command.Services;

namespace Talos.Integration.Command.Abstractions
{
    public interface ICommandFactory
    {
        CommandBuilder Create(string command);
    }
}