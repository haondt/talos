using Haondt.Core.Models;
using Talos.ImageUpdate.Repositories.Shared.Models;
using Talos.ImageUpdate.UpdatePushing.Models;

namespace Talos.ImageUpdate.Repositories.Yaml.Models
{
    public class YamlUpdateLocationCoordinates : IUpdateLocationCoordinates
    {
        public required string RelativeFilePath { get; set; }
        public required int Start { get; set; }
        public required int End { get; set; }
        public UpdateIdentity GetIdentity(string repository, Optional<string> branch)
        {
            return UpdateIdentity.YamlFile(repository, branch, RelativeFilePath, Start, End);
        }

        public override string ToString()
        {
            return $"YamlFile:{RelativeFilePath}:{Start}:{End}";
        }
    }
}
