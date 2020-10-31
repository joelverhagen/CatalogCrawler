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
        private readonly DownloaderConfiguration _config;
        private int _logDepth = 0;

        public Downloader(
            HttpClient httpClient,
            DownloaderConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task DownloadAsync()
        {
            if (_config.Verbose)
            {
                Log($"User-Agent: {_httpClient.DefaultRequestHeaders.UserAgent?.ToString()}");
                Log($"Service index: {_config.ServiceIndexUrl}");
                Log($"Data directory: {_config.DataDirectory}");
                Log($"Depth: {_config.Depth}");
                Log($"JSON formatting: {_config.JsonFormatting}");
                Log($"Max pages: {_config.MaxPages}");
                Log($"Save to disk: {_config.SaveToDisk}");
                Log($"Format paths: {_config.FormatPaths}");
                Log($"Parallel downloads: {_config.ParallelDownloads}");
                Log("Starting..." + Environment.NewLine);
            }

            Log($"Downloading service index: {_config.ServiceIndexUrl}");
            var serviceIndex = await DownloadAndParseAsync<ServiceIndex>(_config.ServiceIndexUrl);
            if (_config.Depth == DownloadDepth.ServiceIndex)
            {
                return;
            }

            const string catalogResourceType = "Catalog/3.0.0";
            var catalogResource = serviceIndex.Value.Resources.SingleOrDefault(x => x.Type == catalogResourceType);
            if (catalogResource == null)
            {
                throw new InvalidOperationException($"No {catalogResourceType} resource was found in '{_config.ServiceIndexUrl}'.");
            }

            var catalogIndexUrl = catalogResource.Url;
            Log($"Downloading catalog index: {catalogIndexUrl}");

            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);
            if (_config.Depth == DownloadDepth.CatalogIndex)
            {
                return;
            }

            var cursor = ReadCursor(catalogIndex.Path);

            var pageItems = GetItems(catalogIndex.Value, cursor);
            if (_config.Verbose)
            {
                Log($"Found {pageItems.Count} pages with new data.");
            }

            _logDepth++;
            var completedPages = 0;
            foreach (var pageItem in pageItems)
            {
                Log($"Downloading catalog page: {pageItem.Url}");
                var page = await DownloadAndParseAsync<CatalogIndex>(pageItem.Url);

                if (_config.Depth == DownloadDepth.CatalogPage)
                {
                    WriteCursor(catalogIndex.Path, pageItem.CommitTimestamp);
                }
                else
                {
                    var leafItems = GetItems(page.Value, cursor);
                    if (_config.Verbose)
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
                                .Range(0, _config.ParallelDownloads)
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
                if (_config.MaxPages.HasValue && completedPages >= _config.MaxPages.Value)
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
            var destPath = GetDestinationPath(leafItem.Url);
            await SaveToDiskAsync(leafItem.Url, destPath);

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
            var cursorPath = GetCursorPath(catalogIndexPath, _config.Depth);

            if (!File.Exists(cursorPath))
            {
                return DateTimeOffset.MinValue;
            }

            var cursor = JsonFileHelper.ReadJson<DateTimeOffset>(cursorPath);
            if (_config.Verbose)
            {
                Log($"Read {_config.Depth} cursor: {cursor:O}");
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
            var cursorPath = GetCursorPath(catalogIndexPath, _config.Depth);
            var cursorDir = Path.GetDirectoryName(cursorPath);
            Directory.CreateDirectory(cursorDir);
            JsonFileHelper.WriteJson(cursorPath, cursor);

            if (_config.Verbose)
            {
                Log($"Wrote {_config.Depth} cursor: {cursor:O}");
            }
        }

        private string GetDestinationPath(string url)
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

            if (_config.FormatPaths)
            {
                pathFormatter.FormatPagePath();
                pathFormatter.FormatLeafPath();
            }

            path = pathFormatter.Path;

            var hostDir = Path.GetFullPath(Path.Combine(_config.DataDirectory, uri.Host));
            var destPath = Path.GetFullPath(Path.Combine(hostDir, path));

            return destPath;
        }

        async Task<string> SaveToDiskAsync(string url, string destPath)
        {
            var destDir = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(destDir);

            await SaveToDiskWithRetryAsync(url, destPath);

            var rewrite = JsonFileHelper.RewriteJson(destPath, _config.JsonFormatting);
            if (_config.Verbose && rewrite)
            {
                Log($"The JSON at path {destPath} was rewritten.");
            }

            return destPath;
        }

        async Task<T> DownloadWithRetryAsync<T>(string url, Func<Stream, Task<T>> processAsync)
        {
            const int maxAttemts = 3;
            for (var i = 0; i < maxAttemts; i++)
            {
                try
                {
                    using var responseStream = await _httpClient.GetStreamAsync(url);
                    return await processAsync(responseStream);
                }
                catch (Exception ex) when (i < maxAttemts - 1)
                {
                    Log($"Retrying download of {url}. Exception:{Environment.NewLine}{ex}");
                }
            }

            throw new InvalidOperationException();
        }

        async Task SaveToDiskWithRetryAsync(string url, string destPath)
        {
            await DownloadWithRetryAsync(
                url,
                async responseStream =>
                {
                    using var fileStream = new FileStream(destPath, FileMode.Create);
                    await responseStream.CopyToAsync(fileStream);
                    return true;
                });
        }

        async Task<T> ParseWithRetryAsync<T>(string url)
        {
            return await DownloadWithRetryAsync(
                url,
                responseStream => Task.FromResult(JsonFileHelper.ReadJson<T>(responseStream)));
        }

        async Task<ParsedFile<T>> DownloadAndParseAsync<T>(string url)
        {
            var destPath = GetDestinationPath(url);
            T value;
            if (_config.SaveToDisk)
            {
                await SaveToDiskAsync(url, destPath);
                value = JsonFileHelper.ReadJson<T>(destPath);
            }
            else
            {
                value = await ParseWithRetryAsync<T>(url);
            }

            return new ParsedFile<T>(destPath, value);
        }
    }
}
