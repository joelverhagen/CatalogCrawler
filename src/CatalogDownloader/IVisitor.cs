using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    interface IVisitor
    {
        Task OnServiceIndexAsync(ServiceIndex serviceIndex);
        Task OnCatalogIndexAsync(CatalogIndex catalogIndex);
        Task OnCatalogPageAsync(CatalogPage catalogIndex);
    }
}
