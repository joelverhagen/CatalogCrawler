using System;

namespace Knapcode.CatalogDownloader
{
    interface IDepthLogger
    {
        IDisposable Indent();
        void LogInformation(string message, params object[] args);
        void LogDebug(string message, params object[] args);
    }
}
