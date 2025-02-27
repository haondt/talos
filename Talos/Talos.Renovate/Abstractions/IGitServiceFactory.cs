namespace Talos.Renovate.Abstractions
{
    public interface IGitServiceFactory
    {
        public Task<IGitService> CreateAsync();
    }
}
