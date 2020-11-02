using CsvHelper;
using Knapcode.CatalogDownloader;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CsvAggregateReportVisitor<TKey, TValue> : IVisitor
    {
        private readonly ICsvAggregateReportUpdater<TKey, TValue> _visitor;
        private readonly string _csvPath;

        public CsvAggregateReportVisitor(ICsvAggregateReportUpdater<TKey, TValue> visitor, string csvPath)
        {
            _visitor = visitor;
            _csvPath = csvPath;
        }

        public async Task OnCatalogPageAsync(CatalogPage catalogPage)
        {
            var records = await _visitor.GetRecordsAsync(catalogPage);
            if (!records.Any())
            {
                return;
            }

            var data = new Dictionary<TKey, TValue>(records);
            if (!File.Exists(_csvPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
            }
            else
            {
                using var readStream = new FileStream(_csvPath, FileMode.Open);
                using var textReader = new StreamReader(readStream);
                using var csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                csvReader.SetDefaultConfiguration();

                csvReader.Read();
                csvReader.ReadHeader();

                while (csvReader.Read())
                {
                    var key = csvReader.GetRecord<KeyRecord>().Key;
                    var existingValue = csvReader.GetRecord<ValueRecord>().Value;

                    if (data.TryGetValue(key, out var newValue))
                    {
                        data[key] = _visitor.Merge(existingValue, newValue);
                    }
                    else
                    {
                        data[key] = existingValue;
                    }
                }
            }

            using var writeStream = new FileStream(_csvPath, FileMode.Create);
            using var textWriter = new StreamWriter(writeStream);
            using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
            csvWriter.SetDefaultConfiguration();

            csvWriter.WriteHeader<KeyRecord>();
            csvWriter.WriteHeader<ValueRecord>();
            csvWriter.NextRecord();
            foreach (var pair in data.OrderBy(x => x.Key, _visitor.KeyComparer))
            {
                csvWriter.WriteRecord(new KeyRecord { Key = pair.Key });
                csvWriter.WriteRecord(new ValueRecord { Value = pair.Value });
                csvWriter.NextRecord();
            }
        }

        private class KeyRecord
        {
            public TKey Key { get; set; }
        }

        private class ValueRecord
        {
            public TValue Value { get; set; }
        }
    }
}
