using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using smart_ia.Dtos;

namespace smart_ia.Services;

public class DataProcessService
{
    public (List<string> Labels, List<decimal> Values) ProcessDataCsv(Stream csvStream, ParametersFilterDto filters)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        csvStream.CopyTo(ms);
        var bytes = ms.ToArray();

        string text = Encoding.UTF8.GetString(bytes);
        if (text.Contains("\uFFFD") || text.Contains('€'))
        {
            text = Encoding.GetEncoding("iso-8859-1").GetString(bytes);
        }

        using var stringReader = new StringReader(text);
        var culturePtBr = new CultureInfo("pt-BR");

        using var csvReader = new CsvReader(stringReader, new CsvConfiguration(culturePtBr)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => sanitizeText(args.Header)
        });

        var records = csvReader.GetRecords<dynamic>().Cast<IDictionary<string, object>>().ToList();

        if (!records.Any())
            return (new List<string>(), new List<decimal>());

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (var row in records)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in row)
            {
                var chaveLimpa = sanitizeText(kvp.Key);
                var valorLimpo = kvp.Value?.ToString() ?? string.Empty;
                dict[chaveLimpa] = valorLimpo;
            }
            cleanRecords.Add(dict);
        }

        string firstKey = cleanRecords.First().Keys.First();
        string secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

        string columnX = sanitizeText(filters.ColumnX ?? firstKey);
        string columnY = sanitizeText(filters.ColumnY ?? secondKey);

        var groups = cleanRecords
            .Where(r => r.ContainsKey(columnX) && r.ContainsKey(columnY))
            .GroupBy(r => sanitizeText(r[columnX]))
            .Select(g => new
            {
                Key = g.Key,
                Values = g.Sum(r => ConvertToDecimal(r[columnY]))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderByDescending(x => x.Values)
            .Take(10)
            .ToList();

        return (
            groups.Select(x => x.Key).ToList(),
            groups.Select(x => x.Values).ToList()
        );
    }

    private static string sanitizeText(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var clean = Regex.Replace(texto, @"[\r\n\t\0]", "").Trim();
        return clean.Replace("\uFFFD", "");
    }

    private static decimal ConvertToDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0m;

        string clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();

        if (string.IsNullOrEmpty(clean)) return 0m;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var valPt))
            return valPt;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var valInv))
            return valInv;

        return 0m;
    }

    public List<(string Title, List<string> Labels, List<decimal> Values)> ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var result = new List<(string, List<string>, List<decimal>)>();
        foreach (System.Data.DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0) continue;
            var (labels, values) = ProcessTable(table, filters);
            result.Add((table.TableName, labels, values));
        }
        return result;
    }

    private (List<string> Labels, List<decimal> Values) ProcessTable(System.Data.DataTable table, ParametersFilterDto filters)
    {
        var headers = table.Columns.Cast<System.Data.DataColumn>()
            .Select(c => sanitizeText(c.ColumnName)).ToList();

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (System.Data.DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
                dict[headers[i]] = row[i]?.ToString() ?? string.Empty;
            cleanRecords.Add(dict);
        }

        string columnX = sanitizeText(filters.ColumnX ?? headers.First());
        string columnY = sanitizeText(filters.ColumnY ?? headers.Skip(1).FirstOrDefault() ?? headers.First());

        var groups = cleanRecords
            .Where(r => r.ContainsKey(columnX) && r.ContainsKey(columnY))
            .GroupBy(r => sanitizeText(r[columnX]))
            .Select(g => new { Key = g.Key, Values = g.Sum(r => ConvertToDecimal(r[columnY])) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderByDescending(x => x.Values).Take(10).ToList();

        return (groups.Select(x => x.Key).ToList(), groups.Select(x => x.Values).ToList());
    }

    public static string DetectGraphicType(List<string> labels, List<decimal> values)
    {
        if (labels.All(l => DateTime.TryParse(l, out _))) return "line";
        if (labels.Count <= 5 && values.Sum() > 0) return "pie";
        return "bar";
    }

    public DatasetMetadataDto ExtractMetadata(List<Dictionary<string, string>> records)
    {
        var metadata = new DatasetMetadataDto
        {
            TotalRows = records.Count
        };

        if (!records.Any()) return metadata;

        var keys = records.First().Keys;

        foreach (var key in keys)
        {
            var sampleValues = records.Select(r => r.GetValueOrDefault(key, "")).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            var uniqueCount = sampleValues.Distinct().Count();

            string type = "text";
            if (sampleValues.Take(10).All(v => decimal.TryParse(v, out _)))
                type = "numeric";
            else if (sampleValues.Take(10).All(v => DateTime.TryParse(v, out _)))
                type = "date";

            metadata.Columns.Add(new ColumnSummaryDto
            {
                Name = key,
                Type = type,
                UniqueValuesCount = uniqueCount,
                SampleValues = sampleValues.Take(3).ToList()
            });
        }
        return metadata;
    }

    public List<Dictionary<string, string>> GetRawRecordsFromCsv(Stream csvStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        csvStream.CopyTo(ms);
        var bytes = ms.ToArray();

        string text = Encoding.UTF8.GetString(bytes);
        if (text.Contains("\uFFFD") || text.Contains('€'))
        {
            text = Encoding.GetEncoding("iso-8859-1").GetString(bytes);
        }

        using var stringReader = new StringReader(text);
        using var csvReader = new CsvReader(stringReader, new CsvConfiguration(new CultureInfo("pt-BR"))
        {
            HasHeaderRecord = true,
            Delimiter = ",",
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => sanitizeText(args.Header)
        });

        var records = csvReader.GetRecords<dynamic>().Cast<IDictionary<string, object>>().ToList();

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (var row in records)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in row)
            {
                dict[sanitizeText(kvp.Key)] = kvp.Value?.ToString() ?? string.Empty;
            }
            cleanRecords.Add(dict);
        }
        return cleanRecords;
    }

    public List<Dictionary<string, string>> GetRawRecordsFromExcel(Stream excelStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var table = dataSet.Tables[0];
        var headers = table.Columns.Cast<System.Data.DataColumn>()
            .Select(c => sanitizeText(c.ColumnName)).ToList();

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (System.Data.DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = row[i]?.ToString() ?? string.Empty;
            }
            cleanRecords.Add(dict);
        }
        return cleanRecords;
    }
}