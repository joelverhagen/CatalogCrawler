using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace Knapcode.CatalogCrawler
{
    class LatestCatalogLeafByPackageVisitor : ICsvAggregateReportUpdater<PackageIdentityKey, LatestCatalogLeafByPackage>
    {
        public ReportName Name => ReportName.LatestCatalogLeafByPackage;
        public IComparer<PackageIdentityKey> KeyComparer => PackageIdentityKeyComparer.Default;

        public Task<IReadOnlyDictionary<PackageIdentityKey, LatestCatalogLeafByPackage>> GetRecordsAsync(CatalogPage catalogPage)
        {
            var result = catalogPage
                .Items
                .GroupBy(x => new PackageIdentityKey
                {
                    PackageId = x.Id,
                    PackageVersion = NuGetVersion.Parse(x.Version)
                })
                .ToDictionary(x => x.Key, ToLatestCatalogLeafByPackage);

            return Task.FromResult((IReadOnlyDictionary<PackageIdentityKey, LatestCatalogLeafByPackage>)result);
        }

        public LatestCatalogLeafByPackage Merge(LatestCatalogLeafByPackage existingValue, LatestCatalogLeafByPackage newValue)
        {
            return newValue;
        }

        private LatestCatalogLeafByPackage ToLatestCatalogLeafByPackage(IGrouping<PackageIdentityKey, CatalogLeafItem> group)
        {
            var latestLeaf = group.OrderByDescending(g => g.CommitTimestamp).First();

            return new LatestCatalogLeafByPackage
            {
                Type = latestLeaf.Type,
                CommitTimestamp = latestLeaf.CommitTimestamp,
            };
        }
    }
}
