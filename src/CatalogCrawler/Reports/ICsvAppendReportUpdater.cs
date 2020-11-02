using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    interface ICsvAppendReportUpdater<T>
    {
        ReportName Name { get; }
        Task<IReadOnlyList<T>> GetRecordsAsync(CatalogPage catalogPage);
    }
}
