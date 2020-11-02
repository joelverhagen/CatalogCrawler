using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class Downloader
    {
        private readonly HttpClient _httpClient;
        private readonly DownloaderConfiguration _config;
        private readonly string _userAgent;
        private readonly ICursorFactory _cursorProvider;
        private readonly IVisitor _visitor;
        private readonly IDepthLogger _logger;

        public Downloader(
            HttpClient httpClient,
            DownloaderConfiguration config,
            ICursorFactory cursorProvider,
            IVisitor visitor,
            IDepthLogger logger)
        {
            _httpClient = httpClient;
            _config = config;
            _userAgent = string.IsNullOrWhiteSpace(config.UserAgent) ? GetUserAgent() : config.UserAgent;
            _cursorProvider = cursorProvider;
            _visitor = visitor;
            _logger = logger;
        }

        public async Task DownloadAsync()
        {
            _logger.LogDebug("Configuration:");
            using (_logger.Indent())
            {
                _logger.LogDebug("User-Agent: {UserAgent}", _userAgent);
                _logger.LogDebug("Service index: {ServiceIndexUrl}", _config.ServiceIndexUrl);
                _logger.LogDebug("Data directory: {DataDirectory}", _config.DataDirectory);
                _logger.LogDebug("Depth: {Depth}", _config.Depth);
                _logger.LogDebug("JSON formatting: {JsonFormatting}", _config.JsonFormatting);
                _logger.LogDebug("Max pages: {MaxPages}", _config.MaxPages);
                _logger.LogDebug("Max commits: {MaxCommits}", _config.MaxCommits);
                _logger.LogDebug("Save to disk: {SaveToDisk}", _config.SaveToDisk);
                _logger.LogDebug("Format paths: {FormatPaths}", _config.FormatPaths);
                _logger.LogDebug("Parallel downloads: {ParallelDownloads}", _config.ParallelDownloads);
            }
            _logger.LogDebug("Starting..." + Environment.NewLine);

            if (_config.MaxCommits.HasValue && _config.Depth < DownloadDepth.CatalogPage)
            {
                throw new InvalidOperationException($"The download depth must be at least {DownloadDepth.CatalogPage} when setting a maximum number of commits.");
            }

            if (_config.MaxPages.HasValue && _config.Depth < DownloadDepth.CatalogIndex)
            {
                throw new InvalidOperationException($"The download depth must be at least {DownloadDepth.CatalogIndex} when setting a maximum number of pages.");
            }

            _logger.LogInformation($"Downloading service index: {_config.ServiceIndexUrl}");
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
            _logger.LogInformation("Downloading catalog index: {Url}", catalogIndexUrl);
            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);

            var cursor = _cursorProvider.GetCursor(catalogIndex.Path);
            cursor.Read();
            FilterItems<CatalogIndex, CatalogPageItem>(catalogIndex, cursor, DateTimeOffset.MaxValue);
            _logger.LogDebug("Found {Count} pages with new data.", catalogIndex.Value.Items.Count);

            if (_config.MaxPages.HasValue
                && _config.MaxPages.Value < catalogIndex.Value.Items.Count)
            {
                catalogIndex.Value.Items = catalogIndex
                    .Value
                    .Items
                    .Take(_config.MaxPages.Value)
                    .ToList();
                _logger.LogInformation("Only processing {Count} new pages, due to max pages limit.", catalogIndex.Value.Items.Count);
            }

            if (_config.Depth == DownloadDepth.CatalogIndex)
            {
                UpdateCursorFromItems<CatalogIndex, CatalogPageItem>(cursor, catalogIndex);
                return;
            }

            using (_logger.Indent())
            {
                var completedCommits = 0;
                foreach (var pageItem in catalogIndex.Value.Items)
                {
                    _logger.LogInformation("Downloading catalog page: {Url}", pageItem.Url);
                    var page = await DownloadAndParseAsync<CatalogPage>(pageItem.Url);

                    FilterItems<CatalogPage, CatalogLeafItem>(page, cursor, pageItem.CommitTimestamp);
                    _logger.LogDebug("Found {Count} new leaves in this page.", page.Value.Items.Count);

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
                            _logger.LogDebug("Only processing {Count} new leaves, due to max commits limit.", page.Value.Items.Count);
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
                            using (_logger.Indent())
                            {
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
                            }
                        }
                    }

                    completedCommits += commitCount;

                    if (_config.MaxCommits.HasValue && completedCommits >= _config.MaxCommits.Value)
                    {
                        _logger.LogInformation("Completed {CompletedCommits} commits. Terminating.", completedCommits);
                        return;
                    }
                }
            }
        }

        void UpdateCursorFromItems<TList, TItem>(ICursor cursor, ParsedFile<TList> parsedFile)
            where TItem : BaseCatalogItem
            where TList : BaseCatalogList<TItem>
        {
            if (parsedFile.Value.Items.Any())
            {
                cursor.Write(parsedFile.Value.Items.Max(x => x.CommitTimestamp));
            }
        }

        async Task DownloadLeafAsync(
            ICursor cursor,
            Dictionary<DateTimeOffset, int> commitTimestampCount,
            BaseCatalogItem leafItem)
        {
            _logger.LogInformation("Downloading catalog leaf: {Url}", leafItem.Url);
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

        static void FilterItems<TList, TItem>(ParsedFile<TList> parsedFile, ICursor cursor, DateTimeOffset max)
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
                _logger.LogDebug("The JSON at path {DestinationPath} was rewritten.", destPath);
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
                    _logger.LogDebug("Retrying download of {Url}. Exception:" + Environment.NewLine + "{Exception}", url, ex);
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
