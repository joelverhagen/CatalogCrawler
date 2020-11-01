namespace Knapcode.CatalogDownloader
{
    interface IDepthLogger
    {
        void LogInformation(int depth, string message, params object[] args);
        void LogDebug(int depth, string message, params object[] args);
    }
}
