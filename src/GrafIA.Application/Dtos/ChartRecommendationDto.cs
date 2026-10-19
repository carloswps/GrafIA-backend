namespace SmartIa.Application.Dtos;

public class ChartRecommendationDto
{
    public string ChartType { get; set; } = string.Empty;
    public string ColumnX { get; set; } = string.Empty;
    public string ColumnY { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
}
