using System;
using System.IO;

namespace Knapcode.CatalogDownloader
{
    class CursorProvider : ICursorProvider
    {
        private readonly string _cursorSuffix;
        private readonly DateTimeOffset _defaultCursorValue;
        private readonly IDepthLogger _logger;

        public CursorProvider(string cursorSuffix, DateTimeOffset defaultCursorValue, IDepthLogger logger)
        {
            _cursorSuffix = cursorSuffix;
            _defaultCursorValue = defaultCursorValue;
            _logger = logger;
        }

        public ICursor GetCursor(string catalogIndexPath)
        {
            var catalogIndexDir = Path.GetDirectoryName(catalogIndexPath);
            var cursorPath = Path.Combine(catalogIndexDir, ".meta", $"cursor.{_cursorSuffix}.json");
            return new Cursor(cursorPath, _defaultCursorValue, _logger);
        }
    }
}
