namespace Knapcode.CatalogCrawler
{
    /// <summary>
    /// The depth of documents to download.
    /// </summary>
    public enum DownloadDepth
    {
        /// <summary>
        /// Only download the service index.
        /// </summary>
        ServiceIndex = 0,

        /// <summary>
        /// Download the service index and catalog index.
        /// </summary>
        CatalogIndex = 1,

        /// <summary>
        /// Download the service index, catalog index, and catalog pages.
        /// </summary>
        CatalogPage = 2,

        /// <summary>
        /// Download the service index, catalog index, catalog pages, and catalog leaves.
        /// </summary>
        CatalogLeaf = 3
    }
}
