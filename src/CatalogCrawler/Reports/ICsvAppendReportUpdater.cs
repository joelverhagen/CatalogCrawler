using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    interface ICsvAppendReportUpdater<T>
    {
        string ReportName { get; }
        Task<IReadOnlyList<T>> GetRecordsAsync(CatalogPage catalogPage);
    }
}
