using System.Collections.Generic;

namespace Knapcode.CatalogDownloader
{
    abstract class BaseCatalogList<T> where T : BaseCatalogItem
    {
        public List<T> Items { get; set; }
    }
}
