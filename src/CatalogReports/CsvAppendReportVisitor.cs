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
    class CsvAppendReportVisitor<T> : IVisitor
    {
        private readonly ICsvAppendReportVisitor<T> _visitor;
        private readonly string _csvPath;

        public CsvAppendReportVisitor(ICsvAppendReportVisitor<T> visitor, string csvPath)
        {
            _visitor = visitor;
            _csvPath = csvPath;
        }

        public async Task OnCatalogPageAsync(CatalogPage catalogPage)
        {
            var records = await _visitor.OnCatalogPageAsync(catalogPage);
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
