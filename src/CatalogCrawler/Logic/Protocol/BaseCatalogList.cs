using System.Collections.Generic;

namespace Knapcode.CatalogCrawler
{
    abstract class BaseCatalogList<T> where T : BaseCatalogItem
    {
        public List<T> Items { get; set; }
    }
}
