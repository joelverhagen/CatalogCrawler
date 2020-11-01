using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class Program
    {
        /// <summary>A tool to download all NuGet catalog documents to a local directory.</summary>
        /// <param name="serviceIndexUrl">The NuGet V3 service index URL.</param>
        /// <param name="dataDir">The directory for storing catalog documents.</param>
        /// <param name="depth">The depth of documents to download.</param>
        /// <param name="jsonFormatting">The setting to use for formatting downloaded JSON.</param>
        /// <param name="maxPages">The maximum number of pages to complete before terminating.</param>
        /// <param name="maxCommits">The maximum number of commits to complete before terminating.</param>
        /// <param name="formatPaths">Format paths to mitigate directories with many files.</param>
        /// <param name="parallelDownloads">The maximum number of parallel downloads.</param>
        /// <param name="verbose">Whether or not to write verbose messages.</param>
        public static async Task Main(
            string serviceIndexUrl = "https://api.nuget.org/v3/index.json",
            string dataDir = "data",
            DownloadDepth depth = DownloadDepth.CatalogLeaf,
            JsonFormatting jsonFormatting = JsonFormatting.Unchanged,
            int? maxPages = null,
            int? maxCommits = null,
            bool formatPaths = false,
            int parallelDownloads = 16,
            bool verbose = false)
        {
            using var httpClient = new HttpClient();

            var consoleLogger = new ConsoleLogger(verbose);
            var depthLogger = new DepthLogger(consoleLogger);

            await ExecuteAsync(
                httpClient,
                serviceIndexUrl,
                dataDir,
                depth,
                jsonFormatting,
                maxPages,
                maxCommits,
                formatPaths,
                parallelDownloads,
                depthLogger);
        }

        public static async Task ExecuteAsync(
            HttpClient httpClient,
            string serviceIndexUrl,
            string dataDir,
            DownloadDepth depth,
            JsonFormatting jsonFormatting,
            int? maxPages,
            int? maxCommits,
            bool formatPaths,
            int parallelDownloads,
            IDepthLogger logger)
        {
            var downloader = new Downloader(
                httpClient,
                new DownloaderConfiguration
                {
                    CursurSuffix = $"download.{depth}",
                    ServiceIndexUrl = serviceIndexUrl,
                    DataDirectory = dataDir,
                    Depth = depth,
                    JsonFormatting = jsonFormatting,
                    MaxPages = maxPages,
                    MaxCommits = maxCommits,
                    SaveToDisk = true,
                    FormatPaths = formatPaths,
                    ParallelDownloads = parallelDownloads,
                },
                NullVisitor.Instance,
                logger);

            await downloader.DownloadAsync();
        }
    }
}
