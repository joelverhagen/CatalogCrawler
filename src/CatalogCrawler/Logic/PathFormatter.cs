using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Knapcode.CatalogCrawler
{
    class PathFormatter
    {
        private readonly List<string> _pieces;

        public PathFormatter(string path)
        {
            _pieces = path.Split('/').ToList();

            if (_pieces.Any(p => p.StartsWith(".")))
            {
                throw new InvalidCastException($"The URL path '{path}' must not segments starting with a period.");
            }
        }

        public string Path => string.Join('/', _pieces);

        /// <summary>
        /// Convert the "{timestamp}/{file}" paths to have slashes between some time segments instead of dots to
        /// reduce the number of items in a single directory level. With this mapping, each timestamp folder will be
        /// grouped into a "year/month/day/hour" parent directory.
        /// </summary>
        public void FormatLeafPath()
        {
            if (_pieces.Count >= 2)
            {
                var match = Regex.Match(_pieces[_pieces.Count - 2], @"^(\d{4})\.(\d{2})\.(\d{2})\.(\d{2})\.(\d{2}\.\d{2})$");
                if (match.Success)
                {
                    _pieces[_pieces.Count - 2] = match.Result("$1/$2/$3/$4/$5");
                }
            }
        }

        /// <summary>
        /// Convert the "pageX.json" paths to be in directories, grouped by 500.
        /// </summary>
        public void FormatPagePath()
        {
            if (_pieces.Count >= 1)
            {
                var match = Regex.Match(_pieces[_pieces.Count - 1], @"^page(\d+)\.json$");
                if (match.Success)
                {
                    var pageNumber = int.Parse(match.Groups[1].Value);
                    const int bucketSize = 500;
                    var min = pageNumber - (pageNumber % bucketSize);
                    var max = min + (bucketSize - 1);
                    var rangePiece = string.Format(CultureInfo.InvariantCulture, "page{0}-page{1}", min, max);
                    _pieces.Insert(_pieces.Count - 1, rangePiece);
                }
            }
        }
    }
}
