using Newtonsoft.Json;
using System;

namespace Knapcode.CatalogCrawler
{
    abstract class BaseCatalogItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        public DateTimeOffset CommitTimestamp { get; set; }
    }
}
