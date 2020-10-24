using Newtonsoft.Json;
using System;

namespace Knapcode.CatalogDownloader
{
    class CatalogItem
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        public DateTimeOffset CommitTimestamp { get; set; }
    }
}
