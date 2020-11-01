using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class Downloader
    {
        private readonly HttpClient _httpClient;
        private readonly DownloaderConfiguration _config;
        private readonly string _userAgent;
        private readonly IVisitor _visitor;
        private readonly IDepthLogger _logger;
        private int _logDepth = 0;

        public Downloader(
            HttpClient httpClient,
            DownloaderConfiguration config,
            IVisitor visitor,
            IDepthLogger logger)
        {
            if (string.IsNullOrWhiteSpace(config.CursurSuffix))
            {
                throw new ArgumentException("The cursor suffix setting must be set.", nameof(config));
            }

            _httpClient = httpClient;
            _config = config;
            _userAgent = string.IsNullOrWhiteSpace(config.UserAgent) ? GetUserAgent() : config.UserAgent;
            _visitor = visitor;
            _logger = logger;
        }

        public async Task DownloadAsync()
        {
            _logDepth = 0;

            LogVerbose("Configuration:");
            _logDepth++;
            LogVerbose("User-Agent: {UserAgent}", _userAgent);
            LogVerbose("Cursor suffix: {CursorSuffix}", _config.CursurSuffix);
            LogVerbose("Service index: {ServiceIndexUrl}", _config.ServiceIndexUrl);
            LogVerbose("Data directory: {DataDirectory}", _config.DataDirectory);
            LogVerbose("Depth: {Depth}", _config.Depth);
            LogVerbose("JSON formatting: {JsonFormatting}", _config.JsonFormatting);
            LogVerbose("Max pages: {MaxPages}", _config.MaxPages);
            LogVerbose("Max commits: {MaxCommits}", _config.MaxCommits);
            LogVerbose("Save to disk: {SaveToDisk}", _config.SaveToDisk);
            LogVerbose("Format paths: {FormatPaths}", _config.FormatPaths);
            LogVerbose("Parallel downloads: {ParallelDownloads}", _config.ParallelDownloads);
            _logDepth--;
            LogVerbose("Starting..." + Environment.NewLine);

            if (_config.MaxCommits.HasValue && _config.Depth < DownloadDepth.CatalogPage)
            {
                throw new InvalidOperationException($"The download depth must be at least {DownloadDepth.CatalogPage} when setting a maximum number of commits.");
            }

            if (_config.MaxPages.HasValue && _config.Depth < DownloadDepth.CatalogIndex)
            {
                throw new InvalidOperationException($"The download depth must be at least {DownloadDepth.CatalogIndex} when setting a maximum number of pages.");
            }

            LogInformation($"Downloading service index: {_config.ServiceIndexUrl}");
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

            await ProcessCatalogAsync(catalogResource.Url);
        }

        static string GetUserAgent()
        {
            var assembly = typeof(Downloader).Assembly;
            var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var userAgent = $"{title}/{version} (+https://github.com/joelverhagen/CatalogDownloader)";
            return userAgent;
        }

        async Task ProcessCatalogAsync(string catalogIndexUrl)
        {
            LogInformation("Downloading catalog index: {Url}", catalogIndexUrl);
            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);

            var cursor = new Cursor(this, catalogIndex.Path);
            cursor.Read();
            FilterItems<CatalogIndex, CatalogPageItem>(catalogIndex, cursor, DateTimeOffset.MaxValue);
            LogVerbose("Found {Count} pages with new data.", catalogIndex.Value.Items.Count);

            if (_config.MaxPages.HasValue
                && _config.MaxPages.Value < catalogIndex.Value.Items.Count)
            {
                catalogIndex.Value.Items = catalogIndex
                    .Value
                    .Items
                    .Take(_config.MaxPages.Value)
                    .ToList();
                LogInformation("Only processing {Count} new pages, due to max pages limit.", catalogIndex.Value.Items.Count);
            }

            await _visitor.OnCatalogIndexAsync(catalogIndex.Value);

            if (_config.Depth == DownloadDepth.CatalogIndex)
            {
                UpdateCursorFromItems<CatalogIndex, CatalogPageItem>(cursor, catalogIndex);
                return;
            }

            _logDepth++;
            var completedCommits = 0;
            foreach (var pageItem in catalogIndex.Value.Items)
            {
                LogInformation("Downloading catalog page: {Url}", pageItem.Url);
                var page = await DownloadAndParseAsync<CatalogPage>(pageItem.Url);

                FilterItems<CatalogPage, CatalogLeafItem>(page, cursor, pageItem.CommitTimestamp);
                LogVerbose("Found {Count} new leaves in this page.", page.Value.Items.Count);

                var pageCommits = page
                    .Value
                    .Items
                    .Select(x => x.CommitTimestamp)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                var commitCount = pageCommits.Count;
                if (_config.MaxCommits.HasValue)
                {
                    var remainingCommits = _config.MaxCommits.Value - completedCommits;
                    if (pageCommits.Count > remainingCommits)
                    {
                        commitCount = remainingCommits;
                        FilterItems<CatalogPage, CatalogLeafItem>(page, cursor, pageCommits[remainingCommits - 1]);
                        LogVerbose("Only processing {Count} new leaves, due to max commits limit.", page.Value.Items.Count);
                    }
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

                completedCommits += commitCount;

                if (_config.MaxCommits.HasValue && completedCommits >= _config.MaxCommits.Value)
                {
                    LogInformation("Completed {CompletedCommits} commits. Terminating.", completedCommits);
                    return;
                }
            }
        }

        static void UpdateCursorFromItems<TList, TItem>(Cursor cursor, ParsedFile<TList> parsedFile)
            where TItem : BaseCatalogItem
            where TList : BaseCatalogList<TItem>
        {
            if (parsedFile.Value.Items.Any())
            {
                cursor.Write(parsedFile.Value.Items.Max(x => x.CommitTimestamp));
            }
        }

        void LogInformation(string message, params object[] data)
        {
            _logger.LogInformation(_logDepth, message, data);
        }

        void LogVerbose(string message, params object[] data)
        {
            _logger.LogDebug(_logDepth, message, data);
        }

        async Task DownloadLeafAsync(
            Cursor cursor,
            Dictionary<DateTimeOffset, int> commitTimestampCount,
            BaseCatalogItem leafItem)
        {
            LogInformation("Downloading catalog leaf: {Url}", leafItem.Url);
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

        string GetDestinationPath(string url)
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
            if (rewrite)
            {
                LogVerbose("The JSON at path {DestinationPath} was rewritten.", destPath);
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
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    using var responseStream = await response.Content.ReadAsStreamAsync();
                    return await processAsync(responseStream);
                }
                catch (Exception ex) when (i < maxAttemts - 1)
                {
                    LogInformation("Retrying download of {Url}. Exception:" + Environment.NewLine + "{Exception}", url, ex);
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
                    Value = _downloader._config.DefaultCursorValue;
                    _downloader.LogVerbose("Cursor {CursurSuffix} does not exist. Using minimum value: {Value:O}", _downloader._config.CursurSuffix, Value);
                }
                else
                {
                    Value = JsonFileHelper.ReadJson<DateTimeOffset>(_cursorPath);
                    _downloader.LogVerbose("Read {CursorSuffix} cursor: {Value:O}", _downloader._config.CursurSuffix, Value);
                }
            }

            public void Write(DateTimeOffset value)
            {
                var cursorDir = Path.GetDirectoryName(_cursorPath);
                Directory.CreateDirectory(cursorDir);
                JsonFileHelper.WriteJson(_cursorPath, value);
                Value = value;
                _downloader.LogVerbose("Wrote {CursurSuffix} cursor: {Value:O}", _downloader._config.CursurSuffix, Value);
            }
        }
    }
}
