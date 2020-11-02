using Knapcode.CatalogDownloader;
using System;
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

        public async Task UpdateAsync<T>(ICsvAppendReportUpdater<T> visitor)
        {
            await UpdateAsync(visitor.ReportName, csvPath => new CsvAppendReportVisitor<T>(visitor, csvPath));
        }

        public async Task UpdateAsync<TKey, TValue>(ICsvAggregateReportUpdater<TKey, TValue> visitor)
        {
            await UpdateAsync(visitor.ReportName, csvPath => new CsvAggregateReportVisitor<TKey, TValue>(visitor, csvPath));
        }

        private async Task UpdateAsync(string reportName, Func<string, IVisitor> getVisitor)
        {
            _logger.LogInformation("Updating report {Name}.", reportName);

            using (_logger.Indent())
            {
                var cursorProvider = new CursorFactory(
                    cursorSuffix: $"report.{reportName}",
                    defaultCursorValue: DateTimeOffset.MinValue,
                    logger: _logger);

                var csvPath = Path.Combine(_config.DataDirectory, "reports", $"{reportName}.csv");

                var downloader = new Downloader(
                    _httpClient,
                    _config,
                    cursorProvider,
                    getVisitor(csvPath),
                    _logger);

                await downloader.DownloadAsync();
            }
        }
    }
}
