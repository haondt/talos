namespace Talos.Renovate.Models
{

    public class TemporaryDirectory : IDisposable
    {
        private readonly bool _normalizeDirectoryOnDispose;

        public string Path { get; private set; }

        public TemporaryDirectory(string? rootPath = null, bool normalizeDirectoryOnDispose = true)
        {
            Path = System.IO.Path.Combine(rootPath ?? System.IO.Path.GetTempPath(), $"talos-{Guid.NewGuid()}");
            Directory.CreateDirectory(Path);
            _normalizeDirectoryOnDispose = normalizeDirectoryOnDispose;
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
                return;
            if (_normalizeDirectoryOnDispose)
            {
                var directory = new DirectoryInfo(Path) { Attributes = FileAttributes.Normal };
                foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                    info.Attributes = FileAttributes.Normal;
            }
            Directory.Delete(Path, true);
        }
    }
}

