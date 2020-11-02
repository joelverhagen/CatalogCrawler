using Knapcode.CatalogDownloader;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class Program
    {
        /// <summary>A tool to incrementally update reports built off of the NuGet catalog.</summary>
        /// <param name="dataDir">The directory for storing cursors and reports.</param>
        /// <param name="maxPages">The maximum number of pages to complete before terminating.</param>
        /// <param name="maxCommits">The maximum number of commits to complete before terminating.</param>
        /// <param name="verbose">Whether or not to write verbose messages.</param>
        static async Task Main(
            string dataDir = "data",
            int? maxPages = null,
            int? maxCommits = null,
            bool verbose = false)
        {
            using var httpClient = new HttpClient();

            var config = new DownloaderConfiguration
            {
                DataDirectory = dataDir,
                MaxPages = maxPages,
                MaxCommits = maxCommits,
            };

            var consoleLogger = new ConsoleLogger(verbose);
            var depthLogger = new DepthLogger(consoleLogger);
            
            var csvReportUpdater = new CsvReportUpdater(httpClient, config, depthLogger);
            await csvReportUpdater.UpdateAsync(new DeletedPackagesReportVisitor());
            await csvReportUpdater.UpdateAsync(new CatalogLeafCountReportVisitor());
        }
    }
}
