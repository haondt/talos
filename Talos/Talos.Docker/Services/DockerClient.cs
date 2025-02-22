using CliWrap.Builders;
using Talos.Docker.Abstractions;
using Talos.Docker.Models;

namespace Talos.Docker.Services
{
    public class DockerClient : IDockerClient
    {
        private readonly DockerClientOptions _options;
        private readonly ICommandFactory _commandFactory;

        public DockerClient(
            DockerClientOptions options,
            ICommandFactory commandFactory)
        {
            _options = options;
            _commandFactory = commandFactory;
        }

        public async Task<List<string>> GetContainersAsync(CancellationToken? cancellationToken = null)
        {
            var result = await PrepareDockerCommand(ab => ab
                    .Add("ps")
                    //.Add("-a")
                    .Add("--format")
                    .Add("{{ .Names }}"))
                .ExecuteAndCaptureStdoutAsync(cancellationToken);
            return result.Trim().Split('\n').ToList();
        }

        private CommandBuilder PrepareCommand(string command, Action<ArgumentsBuilder>? arguments = null)
        {
            switch (_options.HostOptions)
            {
                case LocalDockerHostOptions:
                    {
                        var result = _commandFactory.Create(command);
                        if (arguments != null)
                            result = result.WithArguments(arguments);
                        return result;
                    }
                case SSHDockerHostOptions sshOptions:
                    {
                        string remoteCommand;
                        if (arguments != null)
                        {
                            var ab = new ArgumentsBuilder();
                            arguments.Invoke(ab);
                            remoteCommand = $"{command} {ab.Build()}";
                        }
                        else
                        {
                            remoteCommand = command;
                        }

                        var sensitiveData = new List<string>();
                        var result = _commandFactory.Create("ssh")
                            .WithArguments(ab =>
                            {
                                ab
                                    .Add("-o")
                                    .Add("StrictHostKeyChecking=no")
                                    .Add("-o")
                                    .Add("LogLevel=ERROR");
                                switch (_options.HostOptions)
                                {
                                    case SSHIdentityFileDockerHostOptions identityFileOptions:
                                        ab.Add("-i");
                                        ab.Add(identityFileOptions.IdentityFile);
                                        sensitiveData.Add(identityFileOptions.IdentityFile);
                                        break;
                                    default:
                                        throw new ArgumentException($"Unknown ssh options type: {_options.HostOptions.GetType()}");
                                }
                                ab.Add($"{sshOptions.User}@{sshOptions.Host}");
                                sensitiveData.Add(sshOptions.Host);
                                ab.Add(remoteCommand);
                            });

                        foreach (var sd in sensitiveData)
                            result = result.WithSensitiveDataMasked(sd);
                        return result;
                    }

                default:
                    throw new ArgumentException($"Unknown host options type: {_options.HostOptions.GetType()}");
            }
        }

        private CommandBuilder PrepareDockerCommand(Action<ArgumentsBuilder> arguments)
        {
            return PrepareCommand(DockerConstants.DOCKER_BINARY, arguments);
        }

        //private async Task<CommandResult> RunDockerComposeCommandAsync(string arguments)
        //{
        //    var binary = await _dockerComposeBinary;
        //    var composeCommand = await _dockerVersion switch
        //    {
        //        DockerVersion.V1 => "",
        //        DockerVersion.V2 => "compose",
        //        _ => throw new InvalidOperationException($"Unknown docker version _dockerVersion")
        //    };

        //    return await _runner.RunCommandAsync(binary, $"{composeCommand} {arguments}");
        //}

        //public Task<CommandResult> ComposePull(string containerName)
        //{
        //    return RunDockerComposeCommandAsync($"pull {containerName}");
        //}

        //public Task<CommandResult> ComposeUp(string containerName)
        //{
        //    var forceRecreateFlag = _options.ForceRecreateOnUp ? "--force-recreate" : "";
        //    return RunDockerComposeCommandAsync($"up -d --always-recreate-deps {forceRecreateFlag} {containerName}");
        //}

        //public Task<string> GetImage(string containerName)
        //{
        //    return RunDockerCommandAsync($"inspect --format='{{{{.Config.Image}}}}' {containerName}")
        //        .ContinueWith(cr => cr.Result.AssertSuccessAndGetStdOut());
        //}

        //public Task<string> GetContainerImageDigest(string containerName)
        //{
        //    return RunDockerCommandAsync($"inspect --format='{{{{.Image}}}}' {containerName}")
        //        .ContinueWith(cr => cr.Result.AssertSuccessAndGetStdOut());
        //}
    }
}
