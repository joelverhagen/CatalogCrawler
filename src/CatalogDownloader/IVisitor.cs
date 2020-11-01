using System.Threading.Tasks;

namespace Knapcode.CatalogDownloader
{
    interface IVisitor
    {
        Task OnCatalogPageAsync(CatalogPage catalogPage);
    }
}
