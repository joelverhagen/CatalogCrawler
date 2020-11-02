using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class DownloadCommandHandler : ICommandFactory
    {
        private readonly Action<string> _writeLine;
        private readonly Option<string> _serviceIndexUrlOption;
        private readonly Option<string> _dataDirOption;
        private readonly Option<DownloadDepth> _depthOption;
        private readonly Option<JsonFormatting> _jsonFormattingOption;
        private readonly Option<int?> _maxPagesOption;
        private readonly Option<int?> _maxCommitsOption;
        private readonly Option<bool> _formatPathsOption;
        private readonly Option<int> _parallelDownloadsOption;
        private readonly Option<bool> _verboseOption;

        public DownloadCommandHandler(Action<string> writeLine)
        {
            _writeLine = writeLine;
            _serviceIndexUrlOption = new Option<string>(
                alias: "--service-index-url",
                getDefaultValue: () => "https://api.nuget.org/v3/index.json",
                description: "The NuGet V3 service index URL.");
            _dataDirOption = new Option<string>(
                alias: "--data-dir",
                getDefaultValue: () => "data",
                description: "The directory for storing catalog documents.");
            _depthOption = new Option<DownloadDepth>(
                alias: "--depth",
                getDefaultValue: () => DownloadDepth.CatalogLeaf,
                description: "The depth of documents to download.");
            _jsonFormattingOption = new Option<JsonFormatting>(
                alias: "--json-formatting",
                getDefaultValue: () => JsonFormatting.Unchanged,
                description: "The setting to use for formatting downloaded JSON.");
            _maxPagesOption = new Option<int?>(
                alias: "--max-pages",
                getDefaultValue: () => null,
                description: "The maximum number of pages to complete before terminating.");
            _maxCommitsOption = new Option<int?>(
                alias: "--max-commits",
                getDefaultValue: () => null,
                description: "The maximum number of commits to complete before terminating.");
            _formatPathsOption = new Option<bool>(
                alias: "--format-paths",
                getDefaultValue: () => false,
                description: "Format paths to mitigate directories with many files.");
            _parallelDownloadsOption = new Option<int>(
                alias: "--parallel-downloads",
                getDefaultValue: () => 16,
                description: "The maximum number of parallel downloads.");
            _verboseOption = new Option<bool>(
                alias: "--verbose",
                getDefaultValue: () => false,
                description: "Whether or not to write debug messages.");
        }

        public Command GetCommand()
        {
            var downloadCommand = new Command("download")
            {
                _serviceIndexUrlOption,
                _dataDirOption,
                _depthOption,
                _jsonFormattingOption,
                _maxPagesOption,
                _maxCommitsOption,
                _formatPathsOption,
                _parallelDownloadsOption,
                _verboseOption,
            };

            downloadCommand.Description = "Download NuGet catalog documents to a local directory.";
            downloadCommand.Handler = this;

            return downloadCommand;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            using var httpClient = new HttpClient();

            var consoleLogger = new ConsoleLogger(_writeLine, context.ParseResult.ValueForOption(_verboseOption));
            var depthLogger = new DepthLogger(consoleLogger);

            await ExecuteAsync(
                httpClient,
                context.ParseResult.ValueForOption(_serviceIndexUrlOption),
                context.ParseResult.ValueForOption(_dataDirOption),
                context.ParseResult.ValueForOption(_depthOption),
                context.ParseResult.ValueForOption(_jsonFormattingOption),
                context.ParseResult.ValueForOption(_maxPagesOption),
                context.ParseResult.ValueForOption(_maxCommitsOption),
                context.ParseResult.ValueForOption(_formatPathsOption),
                context.ParseResult.ValueForOption(_parallelDownloadsOption),
                depthLogger);

            return 0;
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
            var cursorProvider = new CursorFactory(
                cursorSuffix: $"download.{depth}",
                defaultCursorValue: DateTimeOffset.MinValue,
                logger: logger);

            var downloader = new Downloader(
                httpClient,
                new DownloaderConfiguration
                {
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
                cursorProvider,
                NullVisitor.Instance,
                logger);

            await downloader.DownloadAsync();
        }
    }
}
