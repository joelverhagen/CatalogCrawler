﻿using Knapcode.CatalogDownloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CatalogLeafCountByTypeReportVisitor : ICsvAggregateReportUpdater<DateTimeOffset, CatalogLeafCountByType>
    {
        public string ReportName => "CatalogLeafCountByType";
        public IComparer<DateTimeOffset> KeyComparer => Comparer<DateTimeOffset>.Default;

        public CatalogLeafCountByType Merge(CatalogLeafCountByType existingValue, CatalogLeafCountByType newValue)
        {
            return new CatalogLeafCountByType
            {
                PackageDetails = existingValue.PackageDetails + newValue.PackageDetails,
                PackageDelete = existingValue.PackageDelete + newValue.PackageDelete,
            };
        }

        public Task<IReadOnlyDictionary<DateTimeOffset, CatalogLeafCountByType>> GetRecordsAsync(CatalogPage catalogPage)
        {
            var result = catalogPage
                .Items
                .GroupBy(x => new DateTimeOffset(x.CommitTimestamp.ToUniversalTime().Date, TimeSpan.Zero))
                .ToDictionary(
                    x => x.Key,
                    x => new CatalogLeafCountByType
                    {
                        PackageDetails = x.Count(x => x.Type == "nuget:PackageDetails"),
                        PackageDelete = x.Count(x => x.Type == "nuget:PackageDelete"),
                    });

            if (result.Sum(x => x.Value.PackageDetails + x.Value.PackageDelete) != catalogPage.Items.Count)
            {
                throw new InvalidOperationException("Not all catalog leaf items had a known type.");
            }

            return Task.FromResult<IReadOnlyDictionary<DateTimeOffset, CatalogLeafCountByType>>(result);
        }
    }
}
