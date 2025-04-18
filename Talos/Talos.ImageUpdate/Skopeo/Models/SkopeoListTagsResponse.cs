namespace Talos.ImageUpdate.Skopeo.Models
{
    public class SkopeoListTagsResponse
    {
        public required string Repository { get; set; }
        public required List<string> Tags { get; set; }
    }
}
