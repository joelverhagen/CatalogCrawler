using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    interface ICsvReportVisitor<T>
    {
        string Name { get; }
        Task<IReadOnlyList<T>> OnCatalogPageAsync(CatalogPage catalogPage);
    }
}
