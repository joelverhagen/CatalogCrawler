using System;
using System.Collections.Generic;

namespace Knapcode.CatalogDownloader
{
    abstract class BaseCatalogList<T> where T : BaseCatalogItem
    {
        public DateTimeOffset CommitTimestamp { get; set; }
        public List<T> Items { get; set; }
    }
}
