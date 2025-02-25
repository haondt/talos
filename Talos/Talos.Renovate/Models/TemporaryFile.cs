namespace Talos.Renovate.Models
{
    public class TemporaryFile : IDisposable
    {
        public string Path { get; private set; }

        public TemporaryFile()
        {
            Path = System.IO.Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (!File.Exists(Path))
                return;
            File.Delete(Path);
        }
    }
}

