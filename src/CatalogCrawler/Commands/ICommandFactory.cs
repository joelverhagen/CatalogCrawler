using System.CommandLine;
using System.CommandLine.Invocation;

namespace Knapcode.CatalogCrawler
{
    interface ICommandFactory : ICommandHandler
    {
        Command GetCommand();
    }
}
