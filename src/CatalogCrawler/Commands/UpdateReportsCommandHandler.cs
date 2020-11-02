using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class UpdateReportsCommandHandler : ICommandFactory
    {
        private readonly Action<string> _writeLine;
        private readonly Option<string> _dataDirOption;
        private readonly Option<int?> _maxPagesOption;
        private readonly Option<int?> _maxCommitsOption;
        private readonly Option<ReportName[]> _reportsOption;
        private readonly Option<DateTimeOffset> _defaultCursorValueOption;
        private readonly Option<bool> _verboseOption;

        public UpdateReportsCommandHandler(Action<string> writeLine)
        {
            _writeLine = writeLine;
            _dataDirOption = new Option<string>(
                alias: "--data-dir",
                getDefaultValue: () => "data",
                description: "The directory for storing cursors and reports.");
            _maxPagesOption = new Option<int?>(
                alias: "--max-pages",
                getDefaultValue: () => null,
                description: "The maximum number of pages to complete before terminating.");
            _maxCommitsOption = new Option<int?>(
                alias: "--max-commits",
                getDefaultValue: () => null,
                description: "The maximum number of commits to complete before terminating.");
            _reportsOption = new Option<ReportName[]>(
                alias: "--reports",
                description: "The reports to update. [default: all]");
            _defaultCursorValueOption = new Option<DateTimeOffset>(
                alias: "--default-cursor-value",
                description: "The default value for a new cursor. [default: DateTimeOffset.MinValue]");
            _verboseOption = new Option<bool>(
                alias: "--verbose",
                getDefaultValue: () => false,
                description: "Whether or not to write debug messages.");
        }

        public Command GetCommand()
        {
            var downloadCommand = new Command("update-reports")
            {
                _dataDirOption,
                _maxPagesOption,
                _maxCommitsOption,
                _reportsOption,
                _defaultCursorValueOption,
                _verboseOption,
            };

            var reportNames = Enum.GetNames(typeof(ReportName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _reportsOption.AddValidator(optionResult =>
            {
                var invalidValues = optionResult
                    .Tokens
                    .Where(x => !reportNames.Contains(x.Value))
                    .Select(x => x.Value)
                    .ToList();
                if (invalidValues.Any())
                {
                    return $"Invalid report names: {string.Join(", ", invalidValues)}";
                }

                return null;
            });

            var possibleReportNames = $"Possible report names are: {string.Join(", ", reportNames.OrderBy(x => x))}";
            downloadCommand.Description = "Incrementally update reports built off of the NuGet catalog. " + possibleReportNames;
            downloadCommand.Handler = this;

            return downloadCommand;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            using var httpClient = new HttpClient();

            var config = new DownloaderConfiguration
            {
                DataDirectory = context.ParseResult.ValueForOption(_dataDirOption),
                MaxPages = context.ParseResult.ValueForOption(_maxPagesOption),
                MaxCommits = context.ParseResult.ValueForOption(_maxCommitsOption),
            };

            var consoleLogger = new ConsoleLogger(_writeLine, context.ParseResult.ValueForOption(_verboseOption));
            var depthLogger = new DepthLogger(consoleLogger);

            var csvReportUpdater = new CsvReportUpdater(
                httpClient,
                config,
                context.ParseResult.ValueForOption(_defaultCursorValueOption),
                depthLogger);

            var reports = context.ParseResult.ValueForOption(_reportsOption);

            if (reports == null || reports.Contains(ReportName.DeletedPackages))
            {
                await csvReportUpdater.UpdateAsync(new DeletedPackagesReportVisitor());
            }

            if (reports == null || reports.Contains(ReportName.CatalogLeafCount))
            {
                await csvReportUpdater.UpdateAsync(new CatalogLeafCountReportVisitor());
            }

            if (reports == null || reports.Contains(ReportName.CatalogLeafCountByType))
            {
                await csvReportUpdater.UpdateAsync(new CatalogLeafCountByTypeReportVisitor());
            }

            return 0;
        }
    }
}
