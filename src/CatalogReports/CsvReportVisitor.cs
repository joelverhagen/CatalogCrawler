using CsvHelper;
using CsvHelper.TypeConversion;
using Knapcode.CatalogDownloader;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CsvReportVisitor<T> : IVisitor
    {
        private readonly ICsvReportVisitor<T> _reportVisitor;
        private readonly string _csvPath;

        public CsvReportVisitor(ICsvReportVisitor<T> reportVisitor, string csvPath)
        {
            _reportVisitor = reportVisitor;
            _csvPath = csvPath;
        }

        public async Task OnCatalogPageAsync(CatalogPage catalogPage)
        {
            var records = await _reportVisitor.OnCatalogPageAsync(catalogPage);
            if (!records.Any())
            {
                return;
            }

            var writeHeader = false;
            if (!File.Exists(_csvPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
                writeHeader = true;
            }

            using var stream = new FileStream(_csvPath, FileMode.Append);
            using var textWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
            var options = new TypeConverterOptions { Formats = new[] { "O" } };
            csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<DateTimeOffset>(options);
            csvWriter.Configuration.HasHeaderRecord = writeHeader;
            csvWriter.WriteRecords(records);
        }
    }
}
