﻿namespace Talos.Docker.Abstractions
{
    public interface IDockerClient
    {
        Task<List<string>> GetContainersAsync();
    }
}