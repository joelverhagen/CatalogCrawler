using System;

namespace Knapcode.CatalogCrawler
{
    interface IDepthLogger
    {
        IDisposable Indent();
        void LogInformation(string message, params object[] args);
        void LogDebug(string message, params object[] args);
    }
}
