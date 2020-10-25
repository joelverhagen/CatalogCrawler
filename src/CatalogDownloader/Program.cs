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
        /// <param name="parallelDownloads">The maximum number of parallel downloads.</param>
        /// <param name="verbose">Whether or not to write verbose messages.</param>
        static async Task<int> Main(
            string serviceIndexUrl = "https://api.nuget.org/v3/index.json",
            string dataDir = "data",
            DownloadDepth depth = DownloadDepth.CatalogLeaf,
            JsonFormatting jsonFormatting = JsonFormatting.PrettyWhenUnindented,
            int parallelDownloads = 16,
            bool verbose = false)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", GetUserAgent());

            var downloader = new Downloader(
                httpClient,
                serviceIndexUrl,
                dataDir,
                depth,
                jsonFormatting,
                parallelDownloads,
                verbose);

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
