namespace SmartIa.Application.Dtos;

public class DatasetMetadataDto
{
    public int TotalRows { get; set; }
    public List<ColumnSummaryDto> Columns { get; set; } = new();
}

public class ColumnSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int UniqueValuesCount { get; set; }
    public List<string> SampleValues { get; set; } = new();
}
