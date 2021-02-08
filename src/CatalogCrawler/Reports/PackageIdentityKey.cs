using System;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using NuGet.Versioning;

namespace Knapcode.CatalogCrawler
{
    record PackageIdentityKey
    {
        public string PackageId { get; init; }

        [TypeConverter(typeof(NuGetVersionConverter))]
        public NuGetVersion PackageVersion { get; init; }
    }

    class PackageIdentityKeyComparer : IComparer<PackageIdentityKey>
    {
        public static readonly PackageIdentityKeyComparer Default = new PackageIdentityKeyComparer();

        public int Compare(PackageIdentityKey x, PackageIdentityKey y)
        {
            // See: https://github.com/NuGet/NuGet.Client/blob/27e64ba7b38abda73ac20173eb6ffaf8b1c2d3d7/src/NuGet.Core/NuGet.Packaging/Core/comparers/PackageIdentityComparer.cs#L89-L117
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (ReferenceEquals(x, null))
            {
                return -1;
            }

            if (ReferenceEquals(y, null))
            {
                return 1;
            }

            int result = StringComparer.OrdinalIgnoreCase.Compare(x.PackageId, y.PackageId);

            if (result == 0)
            {
                result = VersionComparer.Default.Compare(x.PackageVersion, y.PackageVersion);
            }

            return result;
        }
    }
}
