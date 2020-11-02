using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class CsvReportUpdater
    {
        private readonly HttpClient _httpClient;
        private readonly DownloaderConfiguration _config;
        private readonly DateTimeOffset _defaultCursorValue;
        private readonly IDepthLogger _logger;

        public CsvReportUpdater(
            HttpClient httpClient,
            DownloaderConfiguration config,
            DateTimeOffset defaultCursorValue,
            IDepthLogger logger)
        {
            _httpClient = httpClient;
            _config = config;
            _defaultCursorValue = defaultCursorValue;
            _logger = logger;
        }

        public async Task UpdateAsync<T>(ICsvAppendReportUpdater<T> visitor)
        {
            await UpdateAsync(visitor.Name, csvPath => new CsvAppendReportVisitor<T>(visitor, csvPath));
        }

        public async Task UpdateAsync<TKey, TValue>(ICsvAggregateReportUpdater<TKey, TValue> visitor)
        {
            await UpdateAsync(visitor.Name, csvPath => new CsvAggregateReportVisitor<TKey, TValue>(visitor, csvPath));
        }

        private async Task UpdateAsync(ReportName name, Func<string, IVisitor> getVisitor)
        {
            _logger.LogInformation("Updating report {Name}.", name);

            using (_logger.Indent())
            {
                var cursorProvider = new CursorFactory(
                    cursorSuffix: $"report.{name}",
                    defaultCursorValue: _defaultCursorValue,
                    logger: _logger);

                var csvPath = Path.Combine(_config.DataDirectory, "reports", $"{name}.csv");

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
