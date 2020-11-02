namespace Knapcode.CatalogCrawler
{
    /// <summary>
    /// The setting to use for formatting downloaded JSON.
    /// </summary>
    public enum JsonFormatting
    {
        /// <summary>
        /// Only format the file when no indentation is found at the beginning of the file.
        /// </summary>
        PrettyWhenUnindented = 0,

        /// <summary>
        /// Leave the downloaded JSON files as-is.
        /// </summary>
        Unchanged = 1,

        /// <summary>
        /// Always format JSON using consistent JSON indentation settings.
        /// </summary>
        Pretty = 2,

        /// <summary>
        /// Always format the JSON to compact as possible.
        /// </summary>
        Minify = 3,
    }
}
