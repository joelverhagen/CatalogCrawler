using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.IO;
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

        private readonly DefaultWebApplicationFactory<StaticFilesStartup> _factory;
        private readonly string _testDir;
        private readonly string _dataDir;
        private readonly string _webRoot;
        private readonly WebApplicationFactory<StaticFilesStartup> _builder;
        private readonly ConcurrentQueue<string> _paths;
        private readonly HttpClient _httpClient;
        private readonly Downloader _target;

        public DownloadIntegrationTests(DefaultWebApplicationFactory<StaticFilesStartup> factory)
        {
            _factory = factory;
            _testDir = Path.Combine(Path.GetTempPath(), "Knapcode", Guid.NewGuid().ToString());
            _dataDir = Path.Combine(_testDir, "data");
            _webRoot = Path.Combine(_testDir, "wwwroot");

            _builder = _factory.WithWebHostBuilder(b => b
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot(_webRoot));
            _paths = _builder.Services.GetRequiredService<ConcurrentQueue<string>>();
            _httpClient = _builder.CreateClient();
            _target = new Downloader(
                _httpClient,
                serviceIndexUrl: "https://localhost/index.json",
                dataDir: _dataDir,
                parallelDownloads: 1);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _builder.Dispose();
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }

        [Fact]
        public async Task VerifyStep12And3()
        {
            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2);
            CopyFilesToWebRoot(Step3);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step3, "catalog/index.json");
            AssertDownload(Step2, "catalog/page0.json");
            AssertDownload(Step3, "catalog/page1.json");
            AssertDownload(Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertFileContents("\"2020-10-22T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(8, _paths.Count);
        }

        [Fact]
        public async Task VerifyStep1_ThenStep2And3()
        {
            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2);
            CopyFilesToWebRoot(Step3);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step3, "catalog/index.json");
            AssertDownload(Step2, "catalog/page0.json");
            AssertDownload(Step3, "catalog/page1.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertDownload(Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertFileContents("\"2020-10-22T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(7, _paths.Count);
        }

        [Fact]
        public async Task VerifyStep1And2_ThenStep3()
        {
            CopyFilesToWebRoot(Step1);
            CopyFilesToWebRoot(Step2);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step2, "catalog/index.json");
            AssertDownload(Step2, "catalog/page0.json");
            AssertDownload(Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertFileContents("\"2020-10-21T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(6, _paths.Count);
            _paths.Clear();

            await VerifyStep3Async();
        }

        [Fact]
        public async Task VerifyStep1_ThenStep2_ThenStep3()
        {
            await VerifyStep1Async();

            CopyFilesToWebRoot(Step2);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step2, "catalog/index.json");
            AssertDownload(Step2, "catalog/page0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/a.2.0.0.json", "catalog/2020/10/21/00/00.00/a.2.0.0.json");
            AssertDownload(Step2, "catalog/2020.10.21.00.00.00/b.1.0.0.json", "catalog/2020/10/21/00/00.00/b.1.0.0.json");
            AssertFileContents("\"2020-10-21T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(5, _paths.Count);
            _paths.Clear();

            await VerifyStep3Async();
        }

        private async Task VerifyStep3Async()
        {
            CopyFilesToWebRoot(Step3);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step3, "catalog/index.json");
            AssertDownload(Step3, "catalog/page1.json");
            AssertDownload(Step3, "catalog/2020.10.22.00.00.00/b.2.0.0.json", "catalog/2020/10/22/00/00.00/b.2.0.0.json");
            AssertFileContents("\"2020-10-22T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(4, _paths.Count);
            _paths.Clear();
        }

        private async Task VerifyStep1Async()
        {
            CopyFilesToWebRoot(Step1);

            await _target.DownloadAsync();

            AssertDownload(Step1, "index.json");
            AssertDownload(Step1, "catalog/index.json");
            AssertDownload(Step1, "catalog/page0.json");
            AssertDownload(Step1, "catalog/2020.10.20.00.00.00/a.1.0.0.json", "catalog/2020/10/20/00/00.00/a.1.0.0.json");
            AssertFileContents("\"2020-10-20T00:00:00+00:00\"", "catalog/.meta/download-cursor.json");
            Assert.Equal(4, _paths.Count);
            _paths.Clear();
        }

        private void AssertDownload(string testDataDir, string requestPath, string filePath = null)
        {
            if (filePath == null)
            {
                filePath = requestPath;
            }

            Assert.Contains('/' + requestPath, _paths);
            
            var testDataPath = Path.GetFullPath(Path.Combine(testDataDir, requestPath));
            AssertFileContents(File.ReadAllText(testDataPath), filePath);
        }

        private void AssertFileContents(string expected, string filePath)
        {
            var fullFilePath = Path.GetFullPath(Path.Combine(_dataDir, "localhost", filePath));
            Assert.True(File.Exists(fullFilePath), $"A file should exist at path: {fullFilePath}");
            Assert.Equal(expected, File.ReadAllText(fullFilePath));
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
