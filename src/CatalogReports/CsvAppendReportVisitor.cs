using CsvHelper;
using Knapcode.CatalogDownloader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.CatalogReports
{
    class CsvAppendReportVisitor<T> : IVisitor
    {
        private readonly ICsvAppendReportUpdater<T> _visitor;
        private readonly string _csvPath;

        public CsvAppendReportVisitor(ICsvAppendReportUpdater<T> visitor, string csvPath)
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

            var writeHeader = false;
            if (!File.Exists(_csvPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));
                writeHeader = true;
            }

            using var stream = new FileStream(_csvPath, FileMode.Append);
            using var textWriter = new StreamWriter(stream);
            using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
            csvWriter.SetDefaultConfiguration();
            csvWriter.Configuration.HasHeaderRecord = writeHeader;
            csvWriter.WriteRecords(records);
        }
    }
}
