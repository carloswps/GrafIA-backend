namespace SmartIa.Application.Dtos;

public record DataSummaryDto(
    decimal Total, decimal Media, decimal Max, decimal Min, double ShareTop1, double ShareTop3, string ColumnX, string ColumnY
);
