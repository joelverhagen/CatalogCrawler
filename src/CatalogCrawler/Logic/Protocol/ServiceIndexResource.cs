using Newtonsoft.Json;

namespace Knapcode.CatalogCrawler
{
    class ServiceIndexResource
    {
        [JsonProperty("@id")]
        public string Url { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }
    }
}
