using System;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using NuGet.Versioning;

namespace Knapcode.CatalogCrawler
{
    public class NuGetVersionConverter : TypeConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            return NuGetVersion.Parse(text);
        }

        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is not NuGetVersion version)
            {
                throw new ArgumentException($"The value must have a type of {nameof(NuGetVersion)}", nameof(value));
            }

            return version.ToNormalizedString();
        }
    }
}
