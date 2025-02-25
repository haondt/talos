namespace Talos.Renovate.Models
{
    public readonly record struct ImageUpdateIdentity(
            string GitRemoteUrl,
            string RelativeFilePath,
            string ServiceKey)
    {
        public override string ToString()
        {
            return $"{GitRemoteUrl}:{RelativeFilePath}:{ServiceKey}";
        }
    }
}
