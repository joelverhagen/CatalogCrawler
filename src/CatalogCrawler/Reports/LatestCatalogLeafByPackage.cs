using System;

namespace Knapcode.CatalogCrawler
{
    class LatestCatalogLeafByPackage
    {
        /// <summary>
        /// The type of the catalog item: "nuget:PackageDetails" or "nuget:PackageDelete".
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The commit timestamp of this catalog item.
        /// </summary>
        public DateTimeOffset CommitTimestamp { get; set; }
    }
}
