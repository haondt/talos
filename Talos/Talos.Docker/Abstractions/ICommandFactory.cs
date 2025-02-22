using Talos.Docker.Services;

namespace Talos.Docker.Abstractions
{
    public interface ICommandFactory
    {
        CommandBuilder Create(string command);
    }
}