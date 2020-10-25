using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class Downloader
    {
        private readonly HttpClient _httpClient;
        private readonly string _dataDir;
        private readonly DownloadDepth _depth;
        private readonly int _parallelDownloads;
        private readonly string _serviceIndexUrl;

        public Downloader(HttpClient httpClient, string serviceIndexUrl, string dataDir, DownloadDepth depth, int parallelDownloads)
        {
            _httpClient = httpClient;
            _serviceIndexUrl = serviceIndexUrl;
            _dataDir = dataDir;
            _depth = depth;
            _parallelDownloads = parallelDownloads;
        }

        public async Task DownloadAsync()
        {
            Console.WriteLine($"Service index: {_serviceIndexUrl}");
            Console.WriteLine($"Data directory: {_dataDir}");
            Console.WriteLine($"Depth : {_depth}");
            Console.WriteLine($"Parallel downloads: {_parallelDownloads}");
            Console.WriteLine("Starting...");
            Console.WriteLine();

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
            Console.WriteLine($"Catalog index: {catalogIndexUrl}");

            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);
            if (_depth == DownloadDepth.CatalogIndex)
            {
                return;
            }

            var cursor = GetCursor(catalogIndex.Path);
            Console.WriteLine($"Cursor: {cursor:O}");

            var pageItems = GetItems(catalogIndex.Value, cursor);
            Console.WriteLine($"Found {pageItems.Count} pages with new data.");
            foreach (var pageItem in pageItems)
            {
                Console.WriteLine($"Catalog page: {pageItem.Url}");
                var page = await DownloadAndParseAsync<CatalogIndex>(pageItem.Url);

                if (_depth == DownloadDepth.CatalogPage)
                {
                    SetCursor(catalogIndex.Path, pageItem.CommitTimestamp);
                    continue;
                }

                var leafItems = GetItems(page.Value, cursor);
                Console.WriteLine($"Found {leafItems.Count} new leaves in this page.");

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
        }

        async Task DownloadLeafAsync(
            string catalogIndexPath,
            DateTimeOffset pageItemCommitTimestamp,
            Dictionary<DateTimeOffset, int> commitTimestampCount,
            CatalogItem leafItem)
        {
            Console.WriteLine($"Catalog leaf: {leafItem.Url}");
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
                        SetCursor(catalogIndexPath, leafItem.CommitTimestamp);
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

        DateTimeOffset GetCursor(string catalogIndexPath)
        {
            var cursorPath = GetCursorPath(catalogIndexPath);

            if (!File.Exists(cursorPath))
            {
                return DateTimeOffset.MinValue;
            }

            return Parse<DateTimeOffset>(cursorPath);
        }

        string GetCursorPath(string catalogIndexPath)
        {
            var catalogIndexDir = Path.GetDirectoryName(catalogIndexPath);
            return Path.Combine(catalogIndexDir, ".meta", $"cursor.download.{_depth}.json");
        }

        void SetCursor(string catalogIndexPath, DateTimeOffset cursor)
        {
            Console.WriteLine($"Setting cursor: {cursor:O}");
            var cursorPath = GetCursorPath(catalogIndexPath);
            var cursorDir = Path.GetDirectoryName(cursorPath);
            Directory.CreateDirectory(cursorDir);
            using var fileStream = new FileStream(cursorPath, FileMode.Create);
            using var textWriter = new StreamWriter(fileStream);
            using var jsonWriter = new JsonTextWriter(textWriter);
            var serializer = new JsonSerializer();
            serializer.Serialize(jsonWriter, cursor);
        }

        static T Parse<T>(string path)
        {
            using var stream = File.OpenRead(path);
            return Parse<T>(stream);
        }

        static T Parse<T>(Stream stream)
        {
            using var textReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(textReader);
            var serializer = new JsonSerializer();
            return serializer.Deserialize<T>(jsonReader);
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

            var pathPieces = path.Split('/');
            if (pathPieces.Any(p => p.StartsWith(".")))
            {
                throw new InvalidCastException($"The URL path '{path}' must not segments starting with a period.");
            }

            // Convert the {timestamp}/{file} paths to have slashes between some time segments instead of dots to
            // reduce the number of items in a single directory level. With this mapping, each timestamp folder will be
            // grouped into a "year/month/day/hour" parent directory.
            if (pathPieces.Length >= 2)
            {
                var match = Regex.Match(pathPieces[pathPieces.Length - 2], @"^(\d{4})\.(\d{2})\.(\d{2})\.(\d{2})\.(\d{2}\.\d{2})$");
                if (match.Success)
                {
                    pathPieces[pathPieces.Length - 2] = match.Result("$1/$2/$3/$4/$5");
                    path = string.Join("/", pathPieces);
                }
            }

            var hostDir = Path.GetFullPath(Path.Combine(_dataDir, uri.Host));
            var destPath = Path.GetFullPath(Path.Combine(hostDir, path));

            var destDir = Path.GetDirectoryName(destPath);
            Directory.CreateDirectory(destDir);

            await DownloadWithRetryAsync(url, destPath);

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
                    Console.WriteLine($"Retrying download of {url} to {destPath}. Exception:{Environment.NewLine}{ex}");
                }
            }
        }

        async Task<ParsedFile<T>> DownloadAndParseAsync<T>(string url)
        {
            var destPath = await DownloadAsync(url);
            var value = Parse<T>(destPath);
            return new ParsedFile<T>(destPath, value);
        }
    }
}
