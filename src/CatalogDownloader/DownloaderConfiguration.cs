namespace Knapcode.CatalogDownloader
{
    class DownloaderConfiguration
    {
        public string CursurSuffix { get; set; }
        public string ServiceIndexUrl { get; set; }
        public string DataDirectory { get; set; }
        public DownloadDepth Depth { get; set; }
        public JsonFormatting JsonFormatting { get; set; }
        public int? MaxPages { get; set; }
        public int? MaxCommits { get; set; }
        public bool SaveToDisk { get; set; }
        public bool FormatPaths { get; set; }
        public int ParallelDownloads { get; set; }
        public bool Verbose { get; set; }
    }
}
