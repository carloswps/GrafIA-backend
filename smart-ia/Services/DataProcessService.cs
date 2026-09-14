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
    private readonly ILogger<DataProcessService> _logger;

    public DataProcessService(ILogger<DataProcessService> logger)
    {
        _logger = logger;
    }

    public (List<string> Labels, List<decimal> Values, DataSummaryDto Summary) ProcessDataCsv(Stream csvStream,
        ParametersFilterDto filters)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        csvStream.CopyTo(ms);
        var bytes = ms.ToArray();

        var text = Encoding.UTF8.GetString(bytes);
        if (text.Contains("\uFFFD") || text.Contains('€')) text = Encoding.GetEncoding("iso-8859-1").GetString(bytes);

        using var stringReader = new StringReader(text);
        var culturePtBr = new CultureInfo("pt-BR");
        var delimiter = DetectDelimiter(text);

        using var csvReader = new CsvReader(stringReader, new CsvConfiguration(culturePtBr)
        {
            HasHeaderRecord = true,
            Delimiter = delimiter,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => sanitizeText(args.Header)
        });

        var records = csvReader.GetRecords<dynamic>().Cast<IDictionary<string, object>>().ToList();

        if (!records.Any())
            return (new List<string>(), new List<decimal>(),
                new DataSummaryDto(0, 0, 0, 0, 0, 0, filters.ColumnX ?? string.Empty, filters.ColumnY ?? string.Empty));

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

        var firstKey = cleanRecords.First().Keys.First();
        var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

        _logger.LogInformation("CSV: headers=[{Headers}]", string.Join(" | ", cleanRecords.First().Keys));

        var (columnX, columnY) = ResolveColumns(cleanRecords, filters, firstKey, secondKey);
        if (IsGenericHeader(columnY)) columnY = secondKey;
        var displayY = IsGenericHeader(columnY) ? "Valor" : columnY;

        _logger.LogInformation("CSV: ColumnX='{X}' ColumnY='{Y}' displayY='{Display}'", columnX, columnY, displayY);

        var groups = cleanRecords
            .Where(r => r.ContainsKey(columnX) && r.ContainsKey(columnY))
            .GroupBy(r => sanitizeText(r[columnX]))
            .Select(g => new
            {
                Key = g.Key,
                Values = g.Sum(r => ConvertToDecimal(r[columnY]))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !x.Key.Contains(",") && x.Values > 0)
            .OrderByDescending(x => x.Values)
            .Take(10)
            .ToList();

        var labels = groups.Select(x => x.Key).ToList();
        var values = groups.Select(x => x.Values).ToList();

        var summary = BuildSummary(values, columnX, displayY);
        return (labels, values, summary);
    }

    private static DataSummaryDto BuildSummary(List<decimal> values, string columnX, string columnY)
    {
        var total = values.Sum();
        var media = values.Any() ? total / values.Count : 0;
        var max = values.Any() ? values.Max() : 0;
        var min = values.Any() ? values.Min() : 0;
        var top1 = total > 0 && values.Any() ? (double)(values[0] / total * 100) : 0;
        var top3 = total > 0 ? (double)(values.Take(3).Sum() / total * 100) : 0;

        return new DataSummaryDto(total, media, max, min, top1, top3, columnX, columnY);
    }

    private static string DetectDelimiter(string text)
    {
        var firstLine = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ??
                        string.Empty;
        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');

        return semicolons > commas ? ";" : ",";
    }

    private static (string columnX, string columnY) ResolveColumns(List<Dictionary<string, string>> cleanRecords,
        ParametersFilterDto filters, string firstKey, string secondKey)
    {
        var columnX = sanitizeText(filters.ColumnX ?? firstKey);
        var requestedY = string.IsNullOrWhiteSpace(filters.ColumnY) ? null : sanitizeText(filters.ColumnY);

        var columnY = requestedY ?? secondKey;
        if (!cleanRecords.First().ContainsKey(columnY))
            columnY = secondKey;

        if (requestedY == null || !IsNumericColumn(cleanRecords, columnY))
        {
            var detected = DetectNumericColumn(cleanRecords, columnX);
            if (!string.IsNullOrEmpty(detected))
                columnY = detected;
        }

        if (string.Equals(columnX, columnY, StringComparison.OrdinalIgnoreCase))
        {
            var alternativeX = cleanRecords.First().Keys
                .FirstOrDefault(k => !string.Equals(k, columnY, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(alternativeX))
                columnX = alternativeX;
        }

        var priorityNumericColumns = new[] { "Total", "TotalBruto", "Venda Total Liq", "Valor", "Valor Devolvido" };

        foreach (var col in priorityNumericColumns)
            if (cleanRecords.First().ContainsKey(col))
            {
                columnY = col;
                break;
            }

        return (columnX, columnY);
    }

    private static string? DetectNumericColumn(List<Dictionary<string, string>> cleanRecords, string excludeColumn)
    {
        string? best = null;
        double bestRatio = 0;

        foreach (var key in cleanRecords.First().Keys)
        {
            if (string.Equals(key, excludeColumn, StringComparison.OrdinalIgnoreCase))
                continue;

            var sample = cleanRecords.Take(20).Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (!sample.Any())
                continue;

            var ratio = (double)sample.Count(IsParsableDecimal) / sample.Count;
            if (ratio >= 0.5)
                return key;

            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                best = key;
            }
        }

        return bestRatio > 0 ? best : null;
    }

    private static bool IsNumericColumn(List<Dictionary<string, string>> cleanRecords, string column)
    {
        var sample = cleanRecords.Take(20).Select(r => r.GetValueOrDefault(column, ""))
            .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (!sample.Any())
            return false;

        return (double)sample.Count(IsParsableDecimal) / sample.Count >= 0.5;
    }

    private static bool IsParsableDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();

        if (string.IsNullOrEmpty(clean)) return false;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out _))
            return true;

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
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

        var clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();

        if (string.IsNullOrEmpty(clean)) return 0m;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var valPt))
            return valPt;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var valInv))
            return valInv;

        return 0m;
    }

    public List<(string Title, List<string> Labels, List<decimal> Values, DataSummaryDto Summary)>
        ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var result = new List<(string, List<string>, List<decimal>, DataSummaryDto)>();
        foreach (System.Data.DataTable table in dataSet.Tables)
        {
            if (table.Rows.Count == 0) continue;
            var (labels, values, summary) = ProcessTable(table, filters);
            result.Add((table.TableName, labels, values, summary));
        }

        return result;
    }

    private static bool IsGenericHeader(string h)
    {
        return string.IsNullOrWhiteSpace(h) ||
               Regex.IsMatch(h, @"^(Column|Coluna|Unnamed|F)\d*$", RegexOptions.IgnoreCase);
    }

    private (List<string> Labels, List<decimal> Values, DataSummaryDto Summary) ProcessTable(
        System.Data.DataTable table, ParametersFilterDto filters)
    {
        if (table.Rows.Count == 0)
            return (new List<string>(), new List<decimal>(),
                new DataSummaryDto(0, 0, 0, 0, 0, 0, "", ""));

        var cleanRecords = new List<Dictionary<string, string>>();
        List<string> headers;

        var firstRowValues = table.Rows[0].ItemArray.Select(x => x?.ToString() ?? string.Empty).ToList();

        var firstRowIsHeader = firstRowValues.Any(v =>
            v.Contains(",C,") || v.Contains(",N,") || v.Equals("Empresa", StringComparison.OrdinalIgnoreCase));

        var startRowIndex = 0;

        if (firstRowIsHeader)
        {
            headers = firstRowValues.Select(h => CleanHeaderName(h)).ToList();
            startRowIndex = 1;
        }
        else
        {
            headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => CleanHeaderName(c.ColumnName)).ToList();
        }

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

        if (!cleanRecords.Any())
            return (new List<string>(), new List<decimal>(), new DataSummaryDto(0, 0, 0, 0, 0, 0, "", ""));

        var firstKey = cleanRecords.First().Keys.First();
        var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

        _logger.LogInformation("CSV: headers=[{Headers}]", string.Join(" | ", cleanRecords.First().Keys));

        var (columnX, columnY) = ResolveColumns(cleanRecords, filters, firstKey, secondKey);
        if (IsGenericHeader(columnY)) columnY = secondKey;
        var displayY = IsGenericHeader(columnY) ? "Valor" : columnY;

        _logger.LogInformation("Aba '{Table}': ColumnX='{X}' ColumnY='{Y}' displayY='{Display}'",
            table.TableName, columnX, columnY, displayY);

        var groups = cleanRecords
            .Where(r => r.ContainsKey(columnX) && r.ContainsKey(columnY))
            .GroupBy(r => sanitizeText(r[columnX]))
            .Select(g => new
            {
                Key = g.Key,
                Values = g.Sum(r => ConvertToDecimal(r[columnY]))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !x.Key.Contains(",") && x.Values > 0)
            .OrderByDescending(x => x.Values)
            .Take(10)
            .ToList();

        var labels = groups.Select(x => x.Key).ToList();
        var values = groups.Select(x => x.Values).ToList();

        var summary = BuildSummary(values, columnX, displayY);
        return (labels, values, summary);
    }

    private static string CleanHeaderName(string rawHeader)
    {
        if (string.IsNullOrWhiteSpace(rawHeader)) return "Coluna";
        var clean = sanitizeText(rawHeader);
        return clean.Split(',')[0].Trim();
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
            var sampleValues = records.Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            var uniqueCount = sampleValues.Distinct().Count();

            var type = "text";
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
            PrepareHeaderForMatch = args => sanitizeText(args.Header)
        });

        var records = csvReader.GetRecords<dynamic>().Cast<IDictionary<string, object>>().ToList();

        var cleanRecords = new List<Dictionary<string, string>>();
        foreach (var row in records)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in row) dict[sanitizeText(kvp.Key)] = kvp.Value?.ToString() ?? string.Empty;
            cleanRecords.Add(dict);
        }

        return cleanRecords;
    }

    public List<Dictionary<string, string>> GetRawRecordsFromExcel(Stream excelStream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(excelStream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var table = dataSet.Tables[0];
        if (table.Rows.Count == 0) return new List<Dictionary<string, string>>();

        var firstRowValues = table.Rows[0].ItemArray.Select(x => x?.ToString() ?? string.Empty).ToList();
        var firstRowIsHeader = firstRowValues.Any(v =>
            v.Contains(",C,") || v.Contains(",N,") || v.Equals("Empresa", StringComparison.OrdinalIgnoreCase));

        List<string> headers;
        var startRowIndex = 0;

        if (firstRowIsHeader)
        {
            headers = firstRowValues.Select(h => CleanHeaderName(h)).ToList();
            startRowIndex = 1;
        }
        else
        {
            headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => CleanHeaderName(c.ColumnName)).ToList();
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