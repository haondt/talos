using CliWrap.Builders;
using Haondt.Core.Models;
using Talos.Docker.Abstractions;
using Talos.Docker.Models;
using Talos.Integration.Command.Abstractions;
using Talos.Integration.Command.Services;

namespace Talos.Docker.Services
{
    public class DockerClient : IDockerClient
    {
        private readonly DockerClientOptions _options;
        private readonly ICommandFactory _commandFactory;

        private (AbsoluteDateTime CachedAt, List<string> Containers)? _containerListCache;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);

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
            var containers = result.Trim().Split('\n').ToList();
            _containerListCache = (AbsoluteDateTime.Now, containers);
            return containers;
        }
        public async Task<string> GetContainerVersionAsync(string container, CancellationToken? cancellationToken = null)
        {
            var result = await PrepareDockerCommand(ab => ab
                    .Add("inspect")
                    .Add("--format")
                    .Add("{{ index .Config.Labels \"org.opencontainers.image.version\" }}")
                    .Add(container))
                .ExecuteAndCaptureStdoutAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
                throw new ArgumentException("Container is missing label 'org.opencontainers.image.version'.");

            return result.Trim();
        }

        public async Task<string> GetContainerImageNameAsync(string container, CancellationToken? cancellationToken = null)
        {
            var result = await PrepareDockerCommand(ab => ab
                    .Add("inspect")
                    .Add("--format")
                    .Add("{{ .Config.Image }}")
                    .Add(container))
                .ExecuteAndCaptureStdoutAsync(cancellationToken);
            return result.Trim();
        }

        public async Task<string> GetContainerImageDigestAsync(string container, CancellationToken? cancellationToken = null)
        {
            var result = await PrepareDockerCommand(ab => ab
                    .Add("inspect")
                    .Add("--format")
                    .Add("{{ .Image }}")
                    .Add(container))
                .ExecuteAndCaptureStdoutAsync(cancellationToken);
            return result.Trim();
        }

        public async Task<List<string>> GetCachedContainersAsync(CancellationToken? cancellationToken = null)
        {
            var currentCache = _containerListCache;
            if (currentCache.HasValue && AbsoluteDateTime.Now - currentCache.Value.CachedAt < CACHE_DURATION)
                return currentCache.Value.Containers;
            return await GetContainersAsync(cancellationToken);
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
