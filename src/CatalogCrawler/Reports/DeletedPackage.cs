using System;

namespace Knapcode.CatalogCrawler
{
    class DeletedPackage
    {
        public DateTimeOffset CommitTimestamp { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
    }
}
