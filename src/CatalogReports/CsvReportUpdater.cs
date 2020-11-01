using Knapcode.CatalogDownloader;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CsvReportUpdater
    {
        private readonly HttpClient _httpClient;
        private readonly DownloaderConfiguration _config;
        private readonly IDepthLogger _logger;

        public CsvReportUpdater(HttpClient httpClient, DownloaderConfiguration config, IDepthLogger logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task UpdateAsync<T>(ICsvReportVisitor<T> reportVisitor)
        {
            _config.CursurSuffix = $"report.{reportVisitor.Name}";

            var csvPath = Path.Combine(_config.DataDirectory, "reports", $"{reportVisitor.Name}.csv");

            var downloader = new Downloader(
                _httpClient,
                _config,
                new CsvReportVisitor<T>(reportVisitor, csvPath),
                _logger);

            await downloader.DownloadAsync();
        }
    }
}
