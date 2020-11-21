using System.IO;
using Xunit;

namespace Knapcode.CatalogCrawler
{
    public class DirectoryBinsTests
    {
        [Fact]
        public void HashesKeyForLevels()
        {
            var baseDir = Directory.GetCurrentDirectory();
            var key = "newtonsoft.json";
            var expected = Path.Combine(baseDir, "2c", "4c", "f1");

            var actual = DirectoryBins.GetPath(baseDir, 3, key);

            Assert.Equal(expected, actual);
        }
    }
}
