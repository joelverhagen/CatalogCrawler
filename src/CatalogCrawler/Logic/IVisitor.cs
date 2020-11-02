using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    interface IVisitor
    {
        Task OnCatalogPageAsync(CatalogPage catalogPage);
    }
}
