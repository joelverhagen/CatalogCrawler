using System;
using System.Collections.Generic;

namespace Knapcode.CatalogDownloader
{
    class CatalogIndex
    {
        public DateTimeOffset CommitTimestamp { get; set; }
        public List<CatalogItem> Items { get; set; }
    }
}
