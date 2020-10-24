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
        private readonly int _parallelDownloads;
        private readonly string _serviceIndexUrl;

        public Downloader(HttpClient httpClient, string serviceIndexUrl, string dataDir, int parallelDownloads)
        {
            _httpClient = httpClient;
            _serviceIndexUrl = serviceIndexUrl;
            _dataDir = dataDir;
            _parallelDownloads = parallelDownloads;
        }

        public async Task DownloadAsync()
        {
            Console.WriteLine($"Service index: {_serviceIndexUrl}");
            Console.WriteLine($"Data directory: {_dataDir}");
            Console.WriteLine($"Parallel downloads: {_parallelDownloads}");

            var catalogIndexUrl = await GetCatalogIndexUrlAsync();
            Console.WriteLine($"Catalog index: {catalogIndexUrl}");

            var catalogIndex = await DownloadAndParseAsync<CatalogIndex>(catalogIndexUrl);
            var cursor = GetCursor(catalogIndex.Path);
            Console.WriteLine($"Cursor: {cursor:O}");

            var pageItems = GetItems(catalogIndex.Value, cursor);
            Console.WriteLine($"Found {pageItems.Count} pages with new data.");
            foreach (var pageItem in pageItems)
            {
                Console.WriteLine($"Catalog page: {pageItem.Url}");
                var page = await DownloadAndParseAsync<CatalogIndex>(pageItem.Url);
                var leafItems = GetItems(page.Value, cursor);
                Console.WriteLine($"Found {leafItems.Count} new leaves in this page.");

                if (leafItems.Any())
                {
                    var commitTimestampCount = leafItems
                        .GroupBy(x => x.CommitTimestamp)
                        .ToDictionary(x => x.Key, x => x.Count());
                    var work = new ConcurrentBag<CatalogItem>(leafItems);

                    var tasks = Enumerable
                        .Range(0, _parallelDownloads)
                        .Select(async i =>
                        {
                            while (work.TryTake(out var leafItem))
                            {
                                await DownloadLeafAsync(
                                    catalogIndex.Path,
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
                    if (commitTimestampCount.Count == 0 || leafItem.CommitTimestamp < commitTimestampCount.Min(x => x.Key))
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
                .ToList();
        }

        static DateTimeOffset GetCursor(string catalogIndexPath)
        {
            var cursorPath = GetCursorPath(catalogIndexPath);

            if (!File.Exists(cursorPath))
            {
                return DateTimeOffset.MinValue;
            }

            return Parse<DateTimeOffset>(cursorPath);
        }

        static string GetCursorPath(string catalogIndexPath)
        {
            var catalogIndexDir = Path.GetDirectoryName(catalogIndexPath);
            return Path.Combine(catalogIndexDir, ".meta", "cursor.json");
        }

        static void SetCursor(string catalogIndexPath, DateTimeOffset cursor)
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

        async Task<string> GetCatalogIndexUrlAsync()
        {
            const string catalogResourceType = "Catalog/3.0.0";

            using var serviceIndexStream = await _httpClient.GetStreamAsync(_serviceIndexUrl);
            var serviceIndex = Parse<ServiceIndex>(serviceIndexStream);
            var catalogResource = serviceIndex.Resources.FirstOrDefault(x => x.Type == catalogResourceType);
            if (catalogResource == null)
            {
                throw new InvalidOperationException($"No {catalogResourceType} resource was found in '{_serviceIndexUrl}'.");
            }

            return catalogResource.Url;
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

            // Convert the data/{timestamp} directory to have slashes between time digits, not dots, to reduce the
            // number of items in a single directory level. With this change, each timestamp folder will be grouped
            // into a "year/month/day/hour" parent directory.
            if (pathPieces.Length >= 3 && pathPieces[pathPieces.Length - 3] == "data")
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
