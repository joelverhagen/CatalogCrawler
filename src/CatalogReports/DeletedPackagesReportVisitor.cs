using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class DeletedPackagesReportVisitor : ICsvAppendReportVisitor<DeletedPackage>
    {
        public string Name => "DeletedPackages";

        public Task<IReadOnlyList<DeletedPackage>> OnCatalogPageAsync(CatalogPage catalogPage)
        {
            var output = new List<DeletedPackage>();

            foreach (var item in catalogPage.Items)
            {
                if (item.Type == "nuget:PackageDelete")
                {
                    output.Add(new DeletedPackage
                    {
                        CommitTimestamp = item.CommitTimestamp,
                        Id = item.Id,
                        Version = item.Version,
                    });
                }
            }

            return Task.FromResult<IReadOnlyList<DeletedPackage>>(output);
        }
    }
}
