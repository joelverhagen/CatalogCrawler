using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    interface ICsvAggregateReportVisitor<TKey, TValue>
    {
        string Name { get; }
        IComparer<TKey> KeyComparer { get; }
        TValue Merge(TValue existingValue, TValue newValue);
        Task<IReadOnlyDictionary<TKey, TValue>> OnCatalogPageAsync(CatalogPage catalogPage);
    }
}
