using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class Program
    {
        public static Action<string> WriteLine { get; set; }

        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var writeLine = WriteLine ?? (s => Console.WriteLine(s));
            rootCommand.AddCommand(new DownloadCommandHandler(writeLine).GetCommand());
            rootCommand.AddCommand(new UpdateReportsCommandHandler(writeLine).GetCommand());

            return await rootCommand.InvokeAsync(args);
        }
    }
}
