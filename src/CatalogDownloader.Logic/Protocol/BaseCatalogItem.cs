using Newtonsoft.Json;
using System;

namespace Knapcode.CatalogDownloader
{
    abstract class BaseCatalogItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        public DateTimeOffset CommitTimestamp { get; set; }
    }
}
