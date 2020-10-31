using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    class NullVisitor : IVisitor
    {
        public static NullVisitor Instance { get; } = new NullVisitor();

        public Task OnServiceIndexAsync(ServiceIndex serviceIndex) => Task.CompletedTask;
        public Task OnCatalogIndexAsync(CatalogIndex catalogIndex) => Task.CompletedTask;
        public Task OnCatalogPageAsync(CatalogPage catalogIndex) => Task.CompletedTask;
    }
}
