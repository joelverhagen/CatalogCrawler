using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class NullVisitor : IVisitor
    {
        public static NullVisitor Instance { get; } = new NullVisitor();

        public Task OnCatalogPageAsync(CatalogPage catalogPage) => Task.CompletedTask;
    }
}
