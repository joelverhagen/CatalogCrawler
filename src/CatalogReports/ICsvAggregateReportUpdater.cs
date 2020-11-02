using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    interface ICsvAggregateReportUpdater<TKey, TValue>
    {
        string ReportName { get; }
        IComparer<TKey> KeyComparer { get; }
        TValue Merge(TValue existingValue, TValue newValue);
        Task<IReadOnlyDictionary<TKey, TValue>> GetRecordsAsync(CatalogPage catalogPage);
    }
}
