using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class Downloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceIndexUrl;
        private readonly string _dataDir;
        private readonly DownloadDepth _depth;
        private readonly JsonFormatting _jsonFormatting;
        private readonly int _parallelDownloads;
        private readonly int? _maxPages;
        private readonly bool _verbose;
        private int _logDepth = 0;

        public Downloader(
            HttpClient httpClient,
            string serviceIndexUrl,
            string dataDir,
            DownloadDepth depth,
            JsonFormatting jsonFormatting,
            int? maxPages,
            int parallelDownloads,
            bool verbose)
        {
            _httpClient = httpClient;
            _serviceIndexUrl = serviceIndexUrl;
            _dataDir = dataDir;
            _depth = depth;
            _jsonFormatting = jsonFormatting;
            _parallelDownloads = parallelDownloads;
            _maxPages = maxPages;
            _verbose = verbose;
        }

        public async Task DownloadAsync()
        {
            if (_verbose)
            {
                Log($"User-Agent: {_httpClient.DefaultRequestHeaders.UserAgent?.ToString()}");
                Log($"Service index: {_serviceIndexUrl}");
                Log($"Data directory: {_dataDir}");
                Log($"Depth: {_depth}");
                Log($"JSON formatting: {_jsonFormatting}");
                Log($"Max pages: {_maxPages}");
                Log($"Parallel downloads: {_parallelDownloads}");
                Log("Starting..." + Environment.NewLine);
            }

            Log($"Downloading service index: {_serviceIndexUrl}");
            var serviceIndex = await DownloadAndParseAsync<ServiceIndex>(_serviceIndexUrl);
            if (_depth == DownloadDepth.ServiceIndex)
            {
                return;
            }

            const string catalogResourceType = "Catalog/3.0.0";
            var catalogResource = serviceIndex.Value.Resources.SingleOrDefault(x => x.Type == catalogResourceType);
            if (catalogResource == null)
            {
                throw new InvalidOperationException($"No {catalogResourceType} resource was found in '{_serviceIndexUrl}'.");
            }

            var catalogIndexUrl = catalogResource.Url;
            Log($"Downloading catalog index: {catalogIndexUrl}");

            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);
            if (_depth == DownloadDepth.CatalogIndex)
            {
                return;
            }

            var cursor = ReadCursor(catalogIndex.Path);

            var pageItems = GetItems(catalogIndex.Value, cursor);
            if (_verbose)
            {
                Log($"Found {pageItems.Count} pages with new data.");
            }

            _logDepth++;
            var completedPages = 0;
            foreach (var pageItem in pageItems)
            {
                Log($"Downloading catalog page: {pageItem.Url}");
                var page = await DownloadAndParseAsync<CatalogIndex>(pageItem.Url);

                if (_depth == DownloadDepth.CatalogPage)
                {
                    WriteCursor(catalogIndex.Path, pageItem.CommitTimestamp);
                }
                else
                {
                    var leafItems = GetItems(page.Value, cursor);
                    if (_verbose)
                    {
                        Log($"Found {leafItems.Count} new leaves in this page.");
                    }

                    _logDepth++;
                    try
                    {

                        if (leafItems.Any())
                        {
                            var commitTimestampCount = leafItems
                                .GroupBy(x => x.CommitTimestamp)
                                .ToDictionary(x => x.Key, x => x.Count());
                            var work = new ConcurrentQueue<CatalogItem>(leafItems);

                            var tasks = Enumerable
                                .Range(0, _parallelDownloads)
                                .Select(async i =>
                                {
                                    while (work.TryDequeue(out var leafItem))
                                    {
                                        await DownloadLeafAsync(
                                            catalogIndex.Path,
                                            pageItem.CommitTimestamp,
                                            commitTimestampCount,
                                            leafItem);
                                    }
                                })
                                .ToList();
                            await Task.WhenAll(tasks);
                        }
                    }
                    finally
                    {
                        _logDepth--;
                    }
                }

                completedPages++;
                if (_maxPages.HasValue && completedPages >= _maxPages.Value)
                {
                    _logDepth = 0;
                    Log($"Completed {completedPages} pages. Terminating.");
                    return;
                }
            }
        }

        void Log(string message)
        {
            Console.WriteLine(new string(' ', _logDepth * 2) + message);
        }

        async Task DownloadLeafAsync(
            string catalogIndexPath,
            DateTimeOffset pageItemCommitTimestamp,
            Dictionary<DateTimeOffset, int> commitTimestampCount,
            CatalogItem leafItem)
        {
            Log($"Downloading catalog leaf: {leafItem.Url}");
            await DownloadAsync(leafItem.Url);

            lock (commitTimestampCount)
            {
                var newCount = --commitTimestampCount[leafItem.CommitTimestamp];
                if (newCount == 0)
                {
                    commitTimestampCount.Remove(leafItem.CommitTimestamp);

                    // Write the timestamp only it less than the page item commit timestamp to protect against partial
                    // commits to the catalog. Also, only write the timestamp if it's the last item in the commit and
                    // it's the lowest commit timestamp of all pending leaves.
                    if (leafItem.CommitTimestamp <= pageItemCommitTimestamp
                        && (commitTimestampCount.Count == 0 || leafItem.CommitTimestamp < commitTimestampCount.Min(x => x.Key)))
                    {
                        WriteCursor(catalogIndexPath, leafItem.CommitTimestamp);
                    }
                }
            }
        }

        static List<CatalogItem> GetItems(CatalogIndex catalogIndex, DateTimeOffset cursor)
        {
            return catalogIndex
                .Items
                .Where(x => x.CommitTimestamp > cursor)
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.Url)
                .ToList();
        }

        DateTimeOffset ReadCursor(string catalogIndexPath)
        {
            var cursorPath = GetCursorPath(catalogIndexPath, _depth);

            if (!File.Exists(cursorPath))
            {
                return DateTimeOffset.MinValue;
            }

            var cursor = JsonFileHelper.ReadJson<DateTimeOffset>(cursorPath);
            if (_verbose)
            {
                Log($"Read {_depth} cursor: {cursor:O}");
            }

            return cursor;
        }

        static string GetCursorPath(string catalogIndexPath, DownloadDepth depth)
        {
            var catalogIndexDir = Path.GetDirectoryName(catalogIndexPath);
            return Path.Combine(catalogIndexDir, ".meta", $"cursor.download.{depth}.json");
        }

        void WriteCursor(string catalogIndexPath, DateTimeOffset cursor)
        {
            var cursorPath = GetCursorPath(catalogIndexPath, _depth);
            var cursorDir = Path.GetDirectoryName(cursorPath);
            Directory.CreateDirectory(cursorDir);
            JsonFileHelper.WriteJson(cursorPath, cursor);

            if (_verbose)
            {
                Log($"Wrote {_depth} cursor: {cursor:O}");
            }
        }

        async Task<string> DownloadAsync(string url)
        {
            var uri = new Uri(url);
            if (uri.Scheme != "https" || !uri.IsDefaultPort)
            {
                throw new InvalidOperationException($"The URL '{url}' must be HTTPS and use the default port.");
            }

            if (!string.IsNullOrEmpty(uri.Query))
            {
                throw new InvalidOperationException($"No query string is allowed for URL '{url}'.");
            }

            var path = uri.AbsolutePath?.TrimStart('/');
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException($"The URL '{url}' must have a path.");
            }

            var pathFormatter = new PathFormatter(path);
            pathFormatter.FormatPagePath();
            pathFormatter.FormatLeafPath();
            path = pathFormatter.Path;

            var hostDir = Path.GetFullPath(Path.Combine(_dataDir, uri.Host));
            var destPath = Path.GetFullPath(Path.Combine(hostDir, path));

            var destDir = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(destDir);

            await DownloadWithRetryAsync(url, destPath);

            var rewrite = JsonFileHelper.RewriteJson(destPath, _jsonFormatting);
            if (_verbose && rewrite)
            {
                Log($"The JSON at path {destPath} was rewritten.");
            }

            return destPath;
        }

        async Task DownloadWithRetryAsync(string url, string destPath)
        {
            const int maxAttemts = 3;
            for (var i = 0; i < maxAttemts; i++)
            {
                try
                {
                    using var responseStream = await _httpClient.GetStreamAsync(url);
                    using var fileStream = new FileStream(destPath, FileMode.Create);
                    await responseStream.CopyToAsync(fileStream);
                    break;
                }
                catch (Exception ex) when (i < maxAttemts - 1)
                {
                    Log($"Retrying download of {url} to {destPath}. Exception:{Environment.NewLine}{ex}");
                }
            }
        }

        async Task<ParsedFile<T>> DownloadAndParseAsync<T>(string url)
        {
            var destPath = await DownloadAsync(url);
            var value = JsonFileHelper.ReadJson<T>(destPath);
            return new ParsedFile<T>(destPath, value);
        }
    }
}
