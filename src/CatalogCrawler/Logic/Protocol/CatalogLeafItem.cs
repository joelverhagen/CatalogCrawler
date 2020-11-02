using Newtonsoft.Json;

namespace Knapcode.CatalogCrawler
{
    class CatalogLeafItem : BaseCatalogItem
    {
        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("nuget:id")]
        public string Id { get; set; }

        [JsonProperty("nuget:version")]
        public string Version { get; set; }
    }
}
