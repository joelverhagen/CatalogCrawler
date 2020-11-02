using Xunit;

namespace Knapcode.CatalogCrawler
{
    public class PathFormatterTests
    {
        [Theory]
        [InlineData("page-1.json", "page-1.json")]
        [InlineData("page0.json", "page0-page499/page0.json")]
        [InlineData("catalog/page0.json", "catalog/page0-page499/page0.json")]
        [InlineData("page498.json", "page0-page499/page498.json")]
        [InlineData("page499.json", "page0-page499/page499.json")]
        [InlineData("page500.json", "page500-page999/page500.json")]
        [InlineData("page1000.json", "page1000-page1499/page1000.json")]
        public void FormatsPagePath(string path, string expected)
        {
            var target = new PathFormatter(path);
            target.FormatPagePath();
            Assert.Equal(expected, target.Path);
        }

        [Theory]
        [InlineData("2020.10.20.12.30/a.json", "2020.10.20.12.30/a.json")]
        [InlineData("2020/10/20/12/30.15/a.json", "2020/10/20/12/30.15/a.json")]
        [InlineData("2020.10.20.12.30.15/a.json", "2020/10/20/12/30.15/a.json")]
        [InlineData("catalog/2020.10.20.12.30.15/a.json", "catalog/2020/10/20/12/30.15/a.json")]
        public void FormatsLeafPath(string path, string expected)
        {
            var target = new PathFormatter(path);
            target.FormatLeafPath();
            Assert.Equal(expected, target.Path);
        }
    }
}
