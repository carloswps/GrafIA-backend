using Microsoft.Extensions.Logging;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Files;

public class DataProcessService : IDataProcessService
{
    private readonly ILogger<DataProcessService> _logger;

    public DataProcessService(ILogger<DataProcessService> logger)
    {
        _logger = logger;
    }

    public ProcessResultDto ProcessDataCsv(Stream csvStream, ParametersFilterDto filters)
    {
        var cleanRecords = FileRecordsReader.ReadCsv(csvStream);

        if (!cleanRecords.Any())
            return new ProcessResultDto("CSV", new List<string>(), new List<decimal>(),
                new DataSummaryDto(0, 0, 0, 0, 0, 0, filters.ColumnX ?? string.Empty, filters.ColumnY ?? string.Empty),
                0, null, null);

        var firstKey = cleanRecords.First().Keys.First();
        var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

        _logger.LogInformation("CSV: headers=[{Headers}]", string.Join(" | ", cleanRecords.First().Keys));

        return BuildResult("CSV", cleanRecords, filters, firstKey, secondKey);
    }

    public List<Dictionary<string, string>> GetRawRecordsFromCsv(Stream csvStream)
    {
        return FileRecordsReader.ReadCsv(csvStream);
    }

    public List<ProcessResultDto> ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters)
    {
        var result = new List<ProcessResultDto>();

        foreach (var (tableName, cleanRecords) in FileRecordsReader.ReadExcelAll(excelStream))
        {
            var firstKey = cleanRecords.First().Keys.First();
            var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

            _logger.LogInformation("Aba '{Table}': headers=[{Headers}]", tableName, string.Join(" | ", cleanRecords.First().Keys));

            result.Add(BuildResult(tableName, cleanRecords, filters, firstKey, secondKey));
        }

        return result;
    }

    public List<Dictionary<string, string>> GetRawRecordsFromExcel(Stream excelStream)
    {
        return FileRecordsReader.ReadExcelFirst(excelStream);
    }

    public DatasetMetadataDto ExtractMetadata(List<Dictionary<string, string>> records)
    {
        return DatasetAnalyzer.ExtractMetadata(records);
    }


    private (List<string> Labels, List<decimal> Values, DataSummaryDto Summary) Aggregate(
        List<Dictionary<string, string>> cleanRecords, ParametersFilterDto filters, string firstKey, string secondKey)
    {
        var (columnX, columnY) = DatasetAnalyzer.ResolveColumns(cleanRecords, filters, firstKey, secondKey);
        if (DatasetAnalyzer.IsGenericHeader(columnY)) columnY = secondKey;
        var displayY = DatasetAnalyzer.IsGenericHeader(columnY) ? "Valor" : columnY;

        _logger.LogInformation("ColumnX='{X}' ColumnY='{Y}' displayY='{Display}'", columnX, columnY, displayY);

        var (labels, values, totalGroups) = DatasetAnalyzer.GroupTopValues(cleanRecords, columnX, columnY);
        var summary = DatasetAnalyzer.BuildSummary(values, totalGroups, columnX, displayY);

        return (labels, values, summary);
    }

    private ProcessResultDto BuildResult(
        string title, List<Dictionary<string, string>> cleanRecords, ParametersFilterDto filters,
        string firstKey, string secondKey)
    {
        var (labels, values, summary) = Aggregate(cleanRecords, filters, firstKey, secondKey);
        var (periodStart, periodEnd) = InferPeriod(cleanRecords);

        return new ProcessResultDto(title, labels, values, summary, cleanRecords.Count, periodStart, periodEnd);
    }

    private static (DateTime? Start, DateTime? End) InferPeriod(List<Dictionary<string, string>> records)
    {
        if (records.Count == 0) return (null, null);

        string? dateColumn = null;
        foreach (var key in records.First().Keys)
        {
            var sample = records.Take(20)
                .Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            var looksLikeDate = sample.Count > 0 &&
                                sample.All(v => DateTime.TryParse(v, out _)) &&
                                sample.Any(v => v.Contains('/') || v.Contains('-') || v.Contains(':'));

            if (looksLikeDate)
            {
                dateColumn = key;
                break;
            }
        }

        if (dateColumn is null) return (null, null);

        var dates = records
            .Select(r => r.GetValueOrDefault(dateColumn, ""))
            .Select(v => DateTime.TryParse(v, out var d) ? d : (DateTime?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        return dates.Count > 0 ? (dates.Min(), dates.Max()) : (null, null);
    }

    public List<(string TableName, List<Dictionary<string, string>> Records)> GetRawRecordsFromExcelAll(Stream excelStream)
    {
        return FileRecordsReader.ReadExcelAll(excelStream);
    }
}
