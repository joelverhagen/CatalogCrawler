using Microsoft.Extensions.Logging;

namespace Knapcode.CatalogDownloader
{
    class DepthLogger : IDepthLogger
    {
        private readonly ILogger _logger;

        public DepthLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void LogInformation(int depth, string message, params object[] args)
        {
            _logger.LogInformation(new string(' ', 2 * depth) + message, args);
        }

        public void LogDebug(int depth, string message, params object[] args)
        {
            _logger.LogDebug(new string(' ', 2 * depth) + message, args);
        }
    }
}
