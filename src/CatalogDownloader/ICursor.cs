using System;

namespace Knapcode.CatalogDownloader
{
    interface ICursor
    {
        DateTimeOffset Value { get; }

        void Read(int logDepth);
        void Write(int logDepth, DateTimeOffset value);
    }
}