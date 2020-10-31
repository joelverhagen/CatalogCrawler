using System.Net.Http;
using System.Reflection;
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
        /// <param name="formatPaths">Format paths to mitigate directories with many files.</param>
        /// <param name="parallelDownloads">The maximum number of parallel downloads.</param>
        /// <param name="verbose">Whether or not to write verbose messages.</param>
        static async Task<int> Main(
            string serviceIndexUrl = "https://api.nuget.org/v3/index.json",
            string dataDir = "data",
            DownloadDepth depth = DownloadDepth.CatalogLeaf,
            JsonFormatting jsonFormatting = JsonFormatting.Unchanged,
            int? maxPages = null,
            bool formatPaths = false,
            int parallelDownloads = 16,
            bool verbose = false)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", GetUserAgent());

            var downloader = new Downloader(
                httpClient,
                new DownloaderConfiguration
                {
                    ServiceIndexUrl = serviceIndexUrl,
                    DataDirectory = dataDir,
                    Depth = depth,
                    JsonFormatting = jsonFormatting,
                    MaxPages = maxPages,
                    SaveToDisk = true,
                    FormatPaths = formatPaths,
                    ParallelDownloads = parallelDownloads,
                    Verbose = verbose,
                });

            await downloader.DownloadAsync();
            return 0;
        }

        static string GetUserAgent()
        {
            var assembly = typeof(Program).Assembly;
            var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var userAgent = $"{title}/{version}";
            return userAgent;
        }
    }
}
