using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;

namespace SmartIa.Infrastructure.Files;

public static class FileRecordsReader
{
    public static List<Dictionary<string, string>> ReadCsv(Stream csvStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        csvStream.CopyTo(ms);
        var bytes = ms.ToArray();

        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains("\uFFFD") || text.Contains('€')) text = Encoding.GetEncoding("iso-8859-1").GetString(bytes);

        using var stringReader = new StringReader(text);
        var delimiter = DetectDelimiter(text);

        using var csvReader = new CsvReader(stringReader, new CsvConfiguration(new CultureInfo("pt-BR"))
        {
            HasHeaderRecord = true,
            Delimiter = delimiter,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => TextCleaning.SanitizeText(args.Header)
        });

        var records = csvReader.GetRecords<dynamic>().Cast<IDictionary<string, object>>().ToList();

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (var row in records)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in row) dict[TextCleaning.SanitizeText(kvp.Key)] = kvp.Value?.ToString() ?? string.Empty;
            cleanRecords.Add(dict);
        }

        return cleanRecords;
    }

    public static List<(string TableName, List<Dictionary<string, string>> Records)> ReadExcelAll(Stream excelStream)
    {
        var dataSet = ReadExcelDataSet(excelStream);

        var result = new List<(string, List<Dictionary<string, string>>)>();
        foreach (System.Data.DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0) continue;

            var records = ExtractRecordsFromTable(table);
            if (!records.Any()) continue;

            result.Add((table.TableName, records));
        }

        return result;
    }

    public static List<Dictionary<string, string>> ReadExcelFirst(Stream excelStream)
    {
        var dataSet = ReadExcelDataSet(excelStream);

        if (dataSet.Tables.Count == 0) return new List<Dictionary<string, string>>();
        return ExtractRecordsFromTable(dataSet.Tables[0]);
    }

    private static System.Data.DataSet ReadExcelDataSet(Stream excelStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        return reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });
    }

    private static string DetectDelimiter(string text)
    {
        var firstLine = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
                        string.Empty;
        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');

        return semicolons > commas ? ";" : ",";
    }

    private static List<Dictionary<string, string>> ExtractRecordsFromTable(System.Data.DataTable table)
    {
        if (table.Rows.Count == 0) return new List<Dictionary<string, string>>();

        var firstRowValues = table.Rows[0].ItemArray.Select(x => x?.ToString() ?? string.Empty).ToList();

        var firstRowIsHeader = firstRowValues.Any(v =>
            v.Contains(",C,") || v.Contains(",N,") || v.Equals("Empresa", StringComparison.OrdinalIgnoreCase));

        List<string> headers;
        var startRowIndex = 0;

        if (firstRowIsHeader)
        {
            headers = firstRowValues.Select(TextCleaning.CleanHeaderName).ToList();
            startRowIndex = 1;
        }
        else
        {
            headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => TextCleaning.CleanHeaderName(c.ColumnName)).ToList();
        }

        var cleanRecords = new List<Dictionary<string, string>>();
        for (var i = startRowIndex; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                var headerName = headers[c];
                if (!dict.ContainsKey(headerName)) dict[headerName] = row[c]?.ToString() ?? string.Empty;
            }

            cleanRecords.Add(dict);
        }

        return cleanRecords;
    }
}
