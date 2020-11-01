using System;

namespace Knapcode.CatalogDownloader
{
    class DownloaderConfiguration
    {
        public string UserAgent { get; set; }
        public string ServiceIndexUrl { get; set; } = "https://api.nuget.org/v3/index.json";
        public string DataDirectory { get; set; } = "data";
        public DownloadDepth Depth { get; set; } = DownloadDepth.CatalogPage;
        public JsonFormatting JsonFormatting { get; set; } = JsonFormatting.Unchanged;
        public int? MaxPages { get; set; }
        public int? MaxCommits { get; set; }
        public bool SaveToDisk { get; set; } = false;
        public bool FormatPaths { get; set; } = false;
        public int ParallelDownloads { get; set; } = 16;
    }
}
