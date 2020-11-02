using System;
using System.IO;

namespace Knapcode.CatalogDownloader
{
    class Cursor : ICursor
    {
        private readonly string _cursorPath;
        private readonly DateTimeOffset _defaultCursorValue;
        private readonly IDepthLogger _logger;

        public Cursor(string cursorPath, DateTimeOffset defaultCursorValue, IDepthLogger logger)
        {
            _cursorPath = cursorPath;
            _defaultCursorValue = defaultCursorValue;
            _logger = logger;
        }

        public DateTimeOffset Value { get; private set; }

        public void Read()
        {
            if (!File.Exists(_cursorPath))
            {
                Value = _defaultCursorValue;
                _logger.LogDebug("Cursor {Path} does not exist. Using minimum value: {Value:O}", _cursorPath, Value);
            }
            else
            {
                Value = JsonFileHelper.ReadJson<DateTimeOffset>(_cursorPath);
                _logger.LogDebug("Read {Path} cursor: {Value:O}", _cursorPath, Value);
            }
        }

        public void Write(DateTimeOffset value)
        {
            var cursorDir = Path.GetDirectoryName(_cursorPath);
            Directory.CreateDirectory(cursorDir);
            JsonFileHelper.WriteJson(_cursorPath, value);
            Value = value;
            _logger.LogDebug("Wrote {Path} cursor: {Value:O}", _cursorPath, Value);
        }
    }
}
