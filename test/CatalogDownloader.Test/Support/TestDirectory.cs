using System;
using System.IO;

namespace Knapcode.CatalogDownloader
{
    class TestDirectory : IDisposable
    {
        private readonly string _path;

        public TestDirectory()
        {
            _path = Path.Combine(Path.GetTempPath(), "Knapcode", Guid.NewGuid().ToString());
        }

        public static implicit operator string(TestDirectory obj) => obj._path;

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
