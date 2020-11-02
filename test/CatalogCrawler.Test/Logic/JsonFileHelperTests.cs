using System;
using System.IO;
using Xunit;

namespace Knapcode.CatalogCrawler
{
    public class JsonFileHelperTests : IDisposable
    {
        private readonly TestDirectory _testDir;
        private readonly string _path;

        private const string Unindented = "{\"foo\":\"bar\",\"baz\":1}";
        private const string Indented = @"{
  ""foo"": ""bar"",
  ""baz"": 1
}";
        private const string IndentedNotStandard = @"{
 ""foo"":""bar"",
 ""baz"":1
}";

        public JsonFileHelperTests()
        {
            _testDir = new TestDirectory();
            Directory.CreateDirectory(_testDir);
            _path = Path.Combine(_testDir, "test.json");
        }

        [Theory]
        [InlineData(JsonFormatting.PrettyWhenUnindented, Unindented, Indented, true)]
        [InlineData(JsonFormatting.PrettyWhenUnindented, Indented, Indented, false)]
        [InlineData(JsonFormatting.PrettyWhenUnindented, IndentedNotStandard, IndentedNotStandard, false)]
        [InlineData(JsonFormatting.Unchanged, Unindented, Unindented, false)]
        [InlineData(JsonFormatting.Unchanged, Indented, Indented, false)]
        [InlineData(JsonFormatting.Unchanged, IndentedNotStandard, IndentedNotStandard, false)]
        [InlineData(JsonFormatting.Pretty, Unindented, Indented, true)]
        [InlineData(JsonFormatting.Pretty, Indented, Indented, true)]
        [InlineData(JsonFormatting.Pretty, IndentedNotStandard, Indented, true)]
        [InlineData(JsonFormatting.Minify, Unindented, Unindented, true)]
        [InlineData(JsonFormatting.Minify, Indented, Unindented, true)]
        [InlineData(JsonFormatting.Minify, IndentedNotStandard, Unindented, true)]
        public void RewriteJson(JsonFormatting formatting, string input, string expected, bool expectedRewrite)
        {
            File.WriteAllText(_path, input);
            var actualRewrite = JsonFileHelper.RewriteJson(_path, formatting);
            Assert.Equal(expected, File.ReadAllText(_path));
            Assert.Equal(expectedRewrite, actualRewrite);
        }

        public void Dispose()
        {
            _testDir.Dispose();
        }
    }
}
