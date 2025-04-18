namespace Talos.ImageUpdate.Redis.Models
{
    public class RedisSettings
    {
        public int DefaultDatabase { get; set; } = 0;
        public required string Endpoint { get; set; }

    }
}
