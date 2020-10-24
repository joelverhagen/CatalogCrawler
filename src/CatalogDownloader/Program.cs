using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class Program
    {
        /// <summary>A tool to download all NuGet catalog documents to a local directory.</summary>
        /// <param name="serviceIndexUrl">The service index used to discover the catalog index URL.</param>
        /// <param name="dataDir">The directory for storing catalog documents.</param>
        /// <param name="parallelDownloads">The maximum number of parallel downloads.</param>
        static async Task<int> Main(
            string serviceIndexUrl = "https://api.nuget.org/v3/index.json",
            string dataDir = "data",
            int parallelDownloads = 16)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", GetUserAgent());

            var downloader = new Downloader(httpClient, serviceIndexUrl, dataDir, parallelDownloads);
            await downloader.DownloadAsync();
            return 0;
        }

        private static string GetUserAgent()
        {
            var assembly = typeof(Program).Assembly;
            var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var userAgent = $"{title}/{version}";
            return userAgent;
        }
    }
}
