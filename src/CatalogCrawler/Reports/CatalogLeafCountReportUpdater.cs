﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogCrawler
{
    class CatalogLeafCountReportVisitor : ICsvAggregateReportUpdater<DateTimeOffset, int>
    {
        public ReportName Name => ReportName.CatalogLeafCount;
        public IComparer<DateTimeOffset> KeyComparer => Comparer<DateTimeOffset>.Default;

        public int Merge(int existingValue, int newValue)
        {
            return existingValue + newValue;
        }

        public Task<IReadOnlyDictionary<DateTimeOffset, int>> GetRecordsAsync(CatalogPage catalogPage)
        {
            var result = catalogPage
                .Items
                .GroupBy(x => new DateTimeOffset(x.CommitTimestamp.ToUniversalTime().Date, TimeSpan.Zero))
                .ToDictionary(x => x.Key, x => x.Count());
            return Task.FromResult<IReadOnlyDictionary<DateTimeOffset, int>>(result);
        }
    }
}
