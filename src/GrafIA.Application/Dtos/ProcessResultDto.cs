namespace SmartIa.Application.Dtos;

public record ProcessResultDto(
    string Title,
    List<string> Labels,
    List<decimal> Values,
    DataSummaryDto Summary,
    int RowsRead,
    DateTime? PeriodStart,
    DateTime? PeriodEnd
);