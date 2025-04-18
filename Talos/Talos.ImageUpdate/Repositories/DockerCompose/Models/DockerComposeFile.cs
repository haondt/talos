using Talos.ImageUpdate.Shared.Models;
using YamlDotNet.Serialization;

namespace Talos.ImageUpdate.Repositories.DockerCompose.Models
{
    public class DockerComposeFile
    {
        public Dictionary<string, Service>? Services { get; set; }
    }

    public class Service
    {
        public string? Image { get; set; }

        [YamlMember(Alias = "x-talos", ApplyNamingConventions = false)]
        public TalosSettings? XTalos { get; set; }

        // compact form for configuration. some examples:
        // x-tl: x # skip
        // x-tl: ~ # max bump size patch, notify all
        // x-tl: +! # max bump size major, push all
        // x-tl: ^? # max bump size minor, prompt all
        // x-tl: ~:!? # max bump size patch, digest = push, patch = prompt
        // x-tl: @:!!!! # max bump size digest, digest = push, everything else is push but will be ignored because the max is digest
        // x-tl: +:! # max bump size major, digest = push, everything else is the default (notify)
        //
        // in sum:
        // x = skip
        // 1st character is the max bump size (+^~@) for (major, minor, patch, digest)
        // if there is no second character, notify all
        // if the second character is not a colon, then it is the strategy to use for all levels
        //  (*?.!) = notify, prompt, skip, push
        // if the second character is a colon, the following characters specify the strategy for the
        // digest, patch, minor and major in that order. if there are less than 4 characters given, 
        // then we assume prompt for the missing ones
        [YamlMember(Alias = "x-tl", ApplyNamingConventions = false)]
        public string? XTalosShort { get; set; }
    }
}
