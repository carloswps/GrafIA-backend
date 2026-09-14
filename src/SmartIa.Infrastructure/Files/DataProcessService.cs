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

    public (List<string> Labels, List<decimal> Values, DataSummaryDto Summary) ProcessDataCsv(Stream csvStream,
        ParametersFilterDto filters)
    {
        var cleanRecords = FileRecordsReader.ReadCsv(csvStream);

        if (!cleanRecords.Any())
            return (new List<string>(), new List<decimal>(),
                new DataSummaryDto(0, 0, 0, 0, 0, 0, filters.ColumnX ?? string.Empty, filters.ColumnY ?? string.Empty));

        var firstKey = cleanRecords.First().Keys.First();
        var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

        _logger.LogInformation("CSV: headers=[{Headers}]", string.Join(" | ", cleanRecords.First().Keys));

        return Aggregate(cleanRecords, filters, firstKey, secondKey);
    }

    public List<Dictionary<string, string>> GetRawRecordsFromCsv(Stream csvStream)
    {
        return FileRecordsReader.ReadCsv(csvStream);
    }

    public List<(string Title, List<string> Labels, List<decimal> Values, DataSummaryDto Summary)>
        ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters)
    {
        var result = new List<(string, List<string>, List<decimal>, DataSummaryDto)>();

        foreach (var (tableName, cleanRecords) in FileRecordsReader.ReadExcelAll(excelStream))
        {
            var firstKey = cleanRecords.First().Keys.First();
            var secondKey = cleanRecords.First().Keys.Skip(1).FirstOrDefault() ?? firstKey;

            _logger.LogInformation("Aba '{Table}': headers=[{Headers}]",
                tableName, string.Join(" | ", cleanRecords.First().Keys));

            var (labels, values, summary) = Aggregate(cleanRecords, filters, firstKey, secondKey);
            result.Add((tableName, labels, values, summary));
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

        var (labels, values) = DatasetAnalyzer.GroupTopValues(cleanRecords, columnX, columnY);
        var summary = DatasetAnalyzer.BuildSummary(values, columnX, displayY);

        return (labels, values, summary);
    }
}
