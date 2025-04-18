namespace Talos.ImageUpdate.ImageParsing.Models
{
    public enum SemanticVersionSize
    {
        Equal,
        Patch,
        Minor,
        Major,
        Downgrade,
        PrecisionMismatch
    }
}
