using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Knapcode.CatalogCrawler
{
    class DepthLogger : IDepthLogger
    {
        private int _depth;
        private readonly ILogger _logger;

        public DepthLogger(ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable Indent() => new IndentScope(this);

        public void LogInformation(string message, params object[] args)
        {
            _logger.LogInformation(new string(' ', 2 * _depth) + message, args);
        }

        public void LogDebug(string message, params object[] args)
        {
            _logger.LogDebug(new string(' ', 2 * _depth) + message, args);
        }

        private class IndentScope : IDisposable
        {
            private readonly DepthLogger _logger;

            public IndentScope(DepthLogger logger)
            {
                _logger = logger;
                Interlocked.Increment(ref _logger._depth);
            }

            public void Dispose()
            {
                Interlocked.Decrement(ref _logger._depth);
            }
        }
    }
}
