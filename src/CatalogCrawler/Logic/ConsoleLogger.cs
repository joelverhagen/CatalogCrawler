using Microsoft.Extensions.Logging;
using System;

namespace Knapcode.CatalogCrawler
{
    class ConsoleLogger : ILogger
    {
        private readonly Action<string> _writeLine;
        private readonly LogLevel _logLevel;

        public ConsoleLogger(Action<string> writeLine, bool verbose)
        {
            _writeLine = writeLine;
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

            _writeLine(formatter(state, exception));
        }
    }
}
