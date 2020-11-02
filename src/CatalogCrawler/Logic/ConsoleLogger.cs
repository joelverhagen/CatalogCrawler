using Microsoft.Extensions.Logging;
using System;

namespace Knapcode.CatalogCrawler
{
    class ConsoleLogger : ILogger
    {
        private readonly LogLevel _logLevel;

        public ConsoleLogger(bool verbose)
        {
            _logLevel = verbose ? LogLevel.Debug : LogLevel.Information;
        }

        public IDisposable BeginScope<TState>(TState state) => default;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            Console.WriteLine(formatter(state, exception));
        }
    }
}
