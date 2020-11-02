using System;
using System.CommandLine;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class Program
    {
        public static Action<string> WriteLine { get; set; }

        public static async Task<int> Main(string[] args)
        {
#if DEBUG
            const string debugOption = "--debug";
            if (args.Contains(debugOption))
            {
                Debugger.Launch();
                args = args.Where(x => x != debugOption).ToArray();
            }
#endif

            var rootCommand = new RootCommand();

            var writeLine = WriteLine ?? (s => Console.WriteLine(s));
            rootCommand.AddCommand(new DownloadCommandHandler(writeLine).GetCommand());
            rootCommand.AddCommand(new UpdateReportsCommandHandler(writeLine).GetCommand());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
