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
        private readonly IVisitor _visitor;
        private int _logDepth = 0;

        public Downloader(
            HttpClient httpClient,
            DownloaderConfiguration config,
            IVisitor visitor)
        {
            _httpClient = httpClient;
            _config = config;
            _visitor = visitor;
        }

        public async Task DownloadAsync()
        {
            _logDepth = 0;

            if (_config.Verbose)
            {
                Log($"User-Agent: {_httpClient.DefaultRequestHeaders.UserAgent?.ToString()}");
                Log($"Cursor suffix: {_config.CursurSuffix}");
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
            await _visitor.OnServiceIndexAsync(serviceIndex.Value);
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

            var cursor = new Cursor(this, catalogIndex.Path);
            cursor.Read();
            FilterItems<CatalogIndex, CatalogPageItem>(catalogIndex, cursor, DateTimeOffset.MaxValue);
            if (_config.Verbose)
            {
                Log($"Found {catalogIndex.Value.Items.Count} pages with new data.");
            }

            await _visitor.OnCatalogIndexAsync(catalogIndex.Value);
            
            if (_config.Depth == DownloadDepth.CatalogIndex)
            {
                UpdateCursorFromItems<CatalogIndex, CatalogPageItem>(cursor, catalogIndex);
                return;
            }

            _logDepth++;
            var completedPages = 0;
            foreach (var pageItem in catalogIndex.Value.Items)
            {
                Log($"Downloading catalog page: {pageItem.Url}");
                var page = await DownloadAndParseAsync<CatalogPage>(pageItem.Url);

                FilterItems<CatalogPage, CatalogLeafItem>(page, cursor, pageItem.CommitTimestamp);
                if (_config.Verbose)
                {
                    Log($"Found {page.Value.Items.Count} new leaves in this page.");
                }

                await _visitor.OnCatalogPageAsync(page.Value);
                
                if (_config.Depth == DownloadDepth.CatalogPage)
                {
                    UpdateCursorFromItems<CatalogPage, CatalogLeafItem>(cursor, page);
                }
                else
                {
                    if (page.Value.Items.Any())
                    {
                        _logDepth++;

                        var commitTimestampCount = page
                            .Value
                            .Items
                            .GroupBy(x => x.CommitTimestamp)
                            .ToDictionary(x => x.Key, x => x.Count());
                        var work = new ConcurrentQueue<BaseCatalogItem>(page.Value.Items);

                        var tasks = Enumerable
                            .Range(0, _config.ParallelDownloads)
                            .Select(async i =>
                            {
                                while (work.TryDequeue(out var leafItem))
                                {
                                    await DownloadLeafAsync(
                                        cursor,
                                        commitTimestampCount,
                                        leafItem);
                                }
                            })
                            .ToList();
                        await Task.WhenAll(tasks);

                        _logDepth--;
                    }
                }

                completedPages++;
                if (_config.MaxPages.HasValue && completedPages >= _config.MaxPages.Value)
                {
                    Log($"Completed {completedPages} pages. Terminating.");
                    return;
                }
            }
        }

        private static void UpdateCursorFromItems<TList, TItem>(Cursor cursor, ParsedFile<TList> parsedFile)
            where TItem : BaseCatalogItem
            where TList : BaseCatalogList<TItem>
        {
            if (parsedFile.Value.Items.Any())
            {
                cursor.Write(parsedFile.Value.Items.Max(x => x.CommitTimestamp));
            }
        }

        void Log(string message)
        {
            Console.WriteLine(new string(' ', _logDepth * 2) + message);
        }

        async Task DownloadLeafAsync(
            Cursor cursor,
            Dictionary<DateTimeOffset, int> commitTimestampCount,
            BaseCatalogItem leafItem)
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

                    // Only write the timestamp if it's the last item in the commit and
                    // it's the lowest commit timestamp of all pending leaves.
                    if (commitTimestampCount.Count == 0 || leafItem.CommitTimestamp < commitTimestampCount.Min(x => x.Key))
                    {
                        cursor.Write(leafItem.CommitTimestamp);
                    }
                }
            }
        }

        static void FilterItems<TList, TItem>(ParsedFile<TList> parsedFile, Cursor cursor, DateTimeOffset max)
            where TItem : BaseCatalogItem
            where TList : BaseCatalogList<TItem>
        {
            parsedFile.Value.Items = parsedFile
                .Value
                .Items
                .Where(x => x.CommitTimestamp > cursor.Value)
                .Where(x => x.CommitTimestamp <= max)
                .OrderBy(x => x.CommitTimestamp)
                .ThenBy(x => x.Url)
                .ToList();
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

        private class Cursor
        {
            private readonly Downloader _downloader;
            private readonly string _cursorPath;

            public DateTimeOffset Value { get; private set; }

            public Cursor(Downloader downloader, string catalogIndexPath)
            {
                _downloader = downloader;
                var catalogIndexDir = Path.GetDirectoryName(catalogIndexPath);
                _cursorPath = Path.Combine(catalogIndexDir, ".meta", $"cursor.{_downloader._config.CursurSuffix}.json");
            }

            public void Read()
            {
                if (!File.Exists(_cursorPath))
                {
                    Value = DateTimeOffset.MinValue;
                    if (_downloader._config.Verbose)
                    {
                        _downloader.Log($"Cursor {_downloader._config.CursurSuffix} does not exist. Using minimum value: {Value:O}");
                    }
                }
                else
                {
                    Value = JsonFileHelper.ReadJson<DateTimeOffset>(_cursorPath);
                    if (_downloader._config.Verbose)
                    {
                        _downloader.Log($"Read {_downloader._config.CursurSuffix} cursor: {Value:O}");
                    }
                }
            }

            public void Write(DateTimeOffset value)
            {
                var cursorDir = Path.GetDirectoryName(_cursorPath);
                Directory.CreateDirectory(cursorDir);
                JsonFileHelper.WriteJson(_cursorPath, value);
                Value = value;
                if (_downloader._config.Verbose)
                {
                    _downloader.Log($"Wrote {_downloader._config.CursurSuffix} cursor: {Value:O}");
                }
            }
        }
    }
}
