﻿using Haondt.Core.Models;
using Talos.Renovate.Models;

namespace Talos.Renovate.Abstractions
{
    public interface IDockerComposeFileService
    {
        List<(ImageUpdateIdentity Id, TalosSettings Configuration, string Image)> ExtractUpdateTargets(RepositoryConfiguration repository, string clonedRepositoryDirectory);
        (string NewFileContents, Optional<string> PreviousImageString) SetServiceImage(string fileContents, string serviceName, string image);
    }
}