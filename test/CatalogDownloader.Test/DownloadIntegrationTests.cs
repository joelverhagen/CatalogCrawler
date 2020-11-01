using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Knapcode.CatalogDownloader
{
    public class DownloadIntegrationTests : IClassFixture<DefaultWebApplicationFactory<StaticFilesStartup>>, IDisposable
    {
        private const string Step1 = "TestData/Step1";
        private const string Step2 = "TestData/Step2";
        private const string Step3 = "TestData/Step3";
        private const string CursorFormat = "catalog/.meta/cursor.download.{0}.json";

        private readonly DefaultWebApplicationFactory<StaticFilesStartup> _factory;
        private readonly TestDirectory _testDir;
        private readonly string _dataDir;
        private readonly string _webRoot;
        private readonly WebApplicationFactory<StaticFilesStartup> _builder;
        private readonly ConcurrentQueue<string> _paths;
        private readonly HttpClient _httpClient;
        private DownloadDepth _depth;
        private int _expectedRequestCount;

        public DownloadIntegrationTests(DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            _factory = factory;
            _testDir = new TestDirectory();
            _dataDir = Path.Combine(_testDir, "data");
            _webRoot = Path.Combine(_testDir, "wwwroot");

            _builder = _factory.WithWebHostBuilder(b => b
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot(_webRoot));
            _paths = _builder.Services.GetRequiredService<ConcurrentQueue<string>>();
            _httpClient = _builder.CreateClient();
        }

        public static IEnumerable<object[]> AllDepths => Enum
            .GetValues(typeof(DownloadDepth))
            .Cast<DownloadDepth>()
            .Select(x => new object[] { x });

        public void Dispose()
        {
            _httpClient.Dispose();
            _builder.Dispose();
            _testDir.Dispose();
        }

        [Fact(Skip = "Too slow.")]
        public async Task VerifyAgainstNuGetOrg()
        {
            await Program.Main(
                dataDir: _dataDir,
                maxPages: 1,
                verbose: true);

            var baseDir = Path.Combine(_dataDir, "api.nuget.org", "v3");

            AssertFileExists(Path.Combine(baseDir, "index.json"));
            AssertFileExists(Path.Combine(baseDir, "catalog0", "index.json"));
            AssertFileExists(Path.Combine(baseDir, "catalog0", "page0.json"));
            AssertCursor("\"2018-06-09T17:29:27.9281869+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepths))]
        public async Task VerifyStep12And3(DownloadDepth depth)
        {
            _depth = depth;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2);
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-22T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepths))]
        public async Task VerifyStep1_ThenStep2And3(DownloadDepth depth)
        {
            _depth = depth;

            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2);
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-22T00:00:00+00:00\"");
        }

        [Theory]
        [MemberData(nameof(AllDepths))]
        public async Task VerifyStep1And2_ThenStep3(DownloadDepth depth)
        {
            _depth = depth;

            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step2, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-21T00:00:00+00:00\"");

            await VerifyStep3Async();
        }

        [Theory]
        [MemberData(nameof(AllDepths))]
        public async Task VerifyStep1_ThenStep2_ThenStep3(DownloadDepth depth)
        {
            _depth = depth;

            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step2, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step2, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-21T00:00:00+00:00\"");

            await VerifyStep3Async();
        }

        private async Task VerifyStep1Async()
        {
            CopyFilesToWebRoot(Step1);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step1, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step1, "catalog/page0.json", "catalog/page0-page499/page0.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-20T00:00:00+00:00\"");
        }

        private async Task VerifyStep3Async()
        {
            CopyFilesToWebRoot(Step3);

            await ExecuteAsync();

            AssertDownload(DownloadDepth.ServiceIndex, Step1, "index.json");
            AssertDownload(DownloadDepth.CatalogIndex, Step3, "catalog/index.json");
            AssertDownload(DownloadDepth.CatalogPage, Step3, "catalog/page1.json", "catalog/page0-page499/page1.json");
            AssertDownload(DownloadDepth.CatalogLeaf, Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertRequestCount();
            AssertCursor("\"2020-10-22T00:00:00+00:00\"");
        }

        private async Task ExecuteAsync()
        {
            await Program.ExecuteAsync(
                httpClient: _httpClient,
                serviceIndexUrl: "https://localhost/index.json",
                dataDir: _dataDir,
                depth: _depth,
                jsonFormatting: JsonFormatting.Pretty,
                maxPages: null,
                formatPaths: true,
                parallelDownloads: 1,
                verbose: true);
        }

        private void AssertCursor(string value)
        {
            var cursorPath = string.Format(CursorFormat, _depth);
            if (_depth > DownloadDepth.ServiceIndex)
            {
                AssertFile(value, cursorPath);
            }
            else
            {
                var fullCursorPath = GetFullFilePath(cursorPath);
                Assert.False(File.Exists(fullCursorPath), $"The cursor should not exist as path: {fullCursorPath}");
            }
        }

        private void AssertRequestCount()
        {
            Assert.Equal(_expectedRequestCount, _paths.Count);
            _expectedRequestCount = 0;
            _paths.Clear();
        }

        private void AssertDownload(DownloadDepth depth, string testDataDir, string requestPath, string filePath = null)
        {
            if (depth > _depth)
            {
                return;
            }

            if (filePath == null)
            {
                filePath = requestPath;
            }

            Assert.Contains('/' + requestPath, _paths);
            _expectedRequestCount++;
            
            var testDataPath = Path.GetFullPath(Path.Combine(testDataDir, requestPath));
            AssertFile(File.ReadAllText(testDataPath), filePath);
        }

        private void AssertFile(string expected, string filePath)
        {
            var fullFilePath = GetFullFilePath(filePath);
            AssertFileExists(fullFilePath);
            Assert.Equal(expected, File.ReadAllText(fullFilePath));
        }

        private static void AssertFileExists(string fullFilePath)
        {
            Assert.True(File.Exists(fullFilePath), $"A file should exist at path: {fullFilePath}");
        }

        private string GetFullFilePath(string filePath)
        {
            return Path.GetFullPath(Path.Combine(_dataDir, "localhost", filePath));
        }

        private void CopyFilesToWebRoot(string testDataDir)
        {
            var srcDir = Path.GetFullPath(testDataDir);
            foreach (var srcFile in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                var destFile = Path.Combine(_webRoot, srcFile.Substring(srcDir.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(srcFile, destFile, overwrite: true);
            }
        }
    }
}
