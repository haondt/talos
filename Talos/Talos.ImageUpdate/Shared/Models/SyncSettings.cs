namespace Talos.ImageUpdate.Shared.Models
{
    public class SyncSettings
    {
        public required SyncRole Role { get; set; }
        public required string Group { get; set; }
        public required string Id { get; set; }
        public List<string>? Children { get; set; }
        public bool Digest { get; set; } = true;
    }
}
