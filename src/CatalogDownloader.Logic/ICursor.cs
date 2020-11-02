using System;

namespace Knapcode.CatalogDownloader
{
    interface ICursor
    {
        DateTimeOffset Value { get; }

        void Read();
        void Write(DateTimeOffset value);
    }
}