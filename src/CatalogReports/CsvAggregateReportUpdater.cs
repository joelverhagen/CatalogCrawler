using Knapcode.CatalogDownloader;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CsvAggregateReportUpdater
    {
        private readonly HttpClient _httpClient;
        private readonly DownloaderConfiguration _config;
        private readonly IDepthLogger _logger;

        public CsvAggregateReportUpdater(HttpClient httpClient, DownloaderConfiguration config, IDepthLogger logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task UpdateAsync<TKey, TValue>(ICsvAggregateReportVisitor<TKey, TValue> reportVisitor)
        {
            var cursorProvider = new CursorProvider(
                cursorSuffix: $"report.{reportVisitor.Name}",
                defaultCursorValue: DateTimeOffset.MinValue,
                logger: _logger);

            var csvPath = Path.Combine(_config.DataDirectory, "reports", $"{reportVisitor.Name}.csv");

            var downloader = new Downloader(
                _httpClient,
                _config,
                cursorProvider,
                new CsvAggregateReportVisitor<TKey, TValue>(reportVisitor, csvPath),
                _logger);

            await downloader.DownloadAsync();
        }
    }
}
