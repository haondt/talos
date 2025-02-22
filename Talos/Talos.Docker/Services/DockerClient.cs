using Talos.Docker.Abstractions;
using Talos.Docker.Models;

namespace Talos.Docker.Services
{
    public class DockerClient : IDockerClient
    {
        private readonly DockerClientOptions _options;
        private readonly ICommandFactory _commandFactory;
        private string _dockerComposeBinary;


        public DockerClient(
            DockerClientOptions options,
            ICommandFactory commandFactory)
        {
            _options = options;
            _commandFactory = commandFactory;

            _dockerComposeBinary = _options.DockerVersion switch
            {
                DockerVersion.V1 => "docker-compose",
                DockerVersion.V2 => "docker",
                _ => throw new InvalidOperationException($"Unknown docker version {_options.DockerVersion}")
            };
        }

        //private async Task<CommandResult> RunDockerCommandAsync(string arguments)
        //{
        //    await _commandFactory.Create
        //    return await _runner.RunCommandAsync(binary, arguments);
        //}

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
