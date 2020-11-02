using System.IO;
using Xunit;

namespace Knapcode.CatalogCrawler
{
    class DataDirectoryHelper
    {
        private const string CursorFormat = "{0}/.meta/cursor.download.{1}.json";

        private readonly string _dataDir;
        private readonly string _host;

        public DataDirectoryHelper(string dataDir, DownloadDepth depth, string host)
        {
            _dataDir = dataDir;
            _host = host;
            Depth = depth;
        }

        public DownloadDepth Depth { get; set; }

        public void AssertCursor(string dir, string value)
        {
            var cursorPath = string.Format(CursorFormat, dir, Depth);
            if (Depth > DownloadDepth.ServiceIndex)
            {
                AssertFile(value, cursorPath);
            }
            else
            {
                var fullCursorPath = GetFullFilePath(cursorPath);
                Assert.False(File.Exists(fullCursorPath), $"The cursor should not exist as path: {fullCursorPath}");
            }
        }

        public void AssertTestData(string testDataDir, string requestPath, string filePath = null)
        {
            if (filePath == null)
            {
                filePath = requestPath;
            }

            var testDataPath = Path.GetFullPath(Path.Combine(testDataDir, requestPath));
            AssertFile(File.ReadAllText(testDataPath), filePath);
        }

        public void AssertFile(string expected, string filePath)
        {
            AssertFileExists(filePath);
            Assert.Equal(expected, File.ReadAllText(GetFullFilePath(filePath)));
        }

        public void AssertFileExists(string filePath)
        {
            var fullFilePath = GetFullFilePath(filePath);
            Assert.True(File.Exists(fullFilePath), $"A file should exist at path: {fullFilePath}");
        }

        public string GetFullFilePath(string filePath)
        {
            return Path.GetFullPath(Path.Combine(_dataDir, _host, filePath));
        }
    }
}
