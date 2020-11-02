using System;

namespace Knapcode.CatalogCrawler
{
    interface ICursor
    {
        DateTimeOffset Value { get; }

        void Read();
        void Write(DateTimeOffset value);
    }
}