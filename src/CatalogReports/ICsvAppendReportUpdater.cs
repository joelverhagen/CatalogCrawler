using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    interface ICsvAppendReportUpdater<T>
    {
        string ReportName { get; }
        Task<IReadOnlyList<T>> GetRecordsAsync(CatalogPage catalogPage);
    }
}
