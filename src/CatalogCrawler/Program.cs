using System.CommandLine;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            rootCommand.AddCommand(new DownloadCommandHandler().GetCommand());
            rootCommand.AddCommand(new UpdateReportsCommandHandler().GetCommand());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
