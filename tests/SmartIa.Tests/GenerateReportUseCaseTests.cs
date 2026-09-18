using Moq;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;
using SmartIa.Application.UseCases;
using Xunit;

public class GenerateReportUseCaseTests
{
    private static Mock<IAiInsightService> Ai(string? chartType, string insight = "insight")
    {
        var ai = new Mock<IAiInsightService>();
        ai.Setup(a => a.GenerateInsightAsync(
                It.IsAny<List<string>>(), It.IsAny<List<decimal>>(), It.IsAny<DataSummaryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InsightResultDto(chartType, new List<InsightItemDto> { new InsightItemDto("leader", "info", insight) }));
        return ai;
    }

    private static Mock<IDataProcessService> ProcCsv(
        List<string> labels, List<decimal> values, int rowsRead = 123,
        DateTime? start = null, DateTime? end = null)
    {
        var proc = new Mock<IDataProcessService>();
        proc.Setup(p => p.ProcessDataCsv(It.IsAny<Stream>(), It.IsAny<ParametersFilterDto>()))
            .Returns(new ProcessResultDto(
                "CSV", labels, values,
                new DataSummaryDto(100, 100, 100, 100, 100, 100, "Region", "Total"),
                rowsRead, start, end));
        return proc;
    }

    private static Task<ReportResponseDto> Run(Mock<IDataProcessService> proc, Mock<IAiInsightService> ai,
        string fileName = "sales.csv", ParametersFilterDto? filters = null) =>
        new GenerateReportUseCase(proc.Object, ai.Object)
            .ExecuteAsync(new MemoryStream(), fileName, filters ?? new ParametersFilterDto(null, null, null, null));

    [Fact]
    public async Task ExecuteAsync_Csv_GeneratesSingleBlock()
    {
        var result = await Run(
            ProcCsv(new() { "South" }, new() { 100m }, 123, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
            Ai("bar"));

        Assert.Single(result.Blocks);
        Assert.Equal(123, result.Context.RowsProcessed);
        Assert.Equal(new DateTime(2024, 1, 1), result.Context.PeriodStart);
        Assert.Equal(new DateTime(2024, 12, 31), result.Context.PeriodEnd);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitFilter_TakesPriorityOverInferredPeriod()
    {
        var start = new DateTime(2023, 6, 1);
        var end = new DateTime(2023, 6, 30);

        var result = await Run(
            ProcCsv(new() { "South" }, new() { 100m }, 10, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
            Ai("bar"),
            filters: new ParametersFilterDto(start, end, null, null));

        Assert.Equal(start, result.Context.PeriodStart);
        Assert.Equal(end, result.Context.PeriodEnd);
    }

    [Fact]
    public async Task ExecuteAsync_Excel_SumsRowsAcrossAllWorksheets()
    {
        var proc = new Mock<IDataProcessService>();
        proc.Setup(p => p.ProcessDataExcelMulti(It.IsAny<Stream>(), It.IsAny<ParametersFilterDto>()))
            .Returns(new List<ProcessResultDto>
            {
                new("SALES", new() { "South" }, new() { 100m },
                    new DataSummaryDto(100, 100, 100, 100, 100, 100, "Region", "Total"), 50, null, null),
                new("RETURNS", new() { "South" }, new() { 10m },
                    new DataSummaryDto(10, 10, 10, 10, 100, 100, "Region", "ReturnedAmount"), 25, null, null)
            });

        var result = await Run(proc, Ai("bar"), "report.xlsx");

        Assert.Equal(2, result.Blocks.Count);
        Assert.Equal(75, result.Context.RowsProcessed);
    }

    // ---------- Scope B: AI suggests, heuristic overrides/validates ----------

    [Fact]
    public async Task ExecuteAsync_AiSuggestsAllowedType_UsesAiSuggestion()
    {
        // 3 categories with balanced distribution -> heuristic would output "pie",
        // but AI requested "bar" and it is allowed.
        var result = await Run(ProcCsv(new() { "South", "North", "East" }, new() { 100m, 50m, 25m }), Ai("bar"));

        Assert.Equal("bar", result.Blocks[0].TypeGraphic);
    }

    [Fact]
    public async Task ExecuteAsync_AiSuggestsKpiWithMultipleCategories_HeuristicVetoes()
    {
        // "kpi" with 3 categories is invalid -> falls back to Detect (pie)
        var result = await Run(ProcCsv(new() { "South", "North", "East" }, new() { 100m, 50m, 25m }), Ai("kpi"));

        Assert.NotEqual("kpi", result.Blocks[0].TypeGraphic);
        Assert.Equal("pie", result.Blocks[0].TypeGraphic);
    }

    [Fact]
    public async Task ExecuteAsync_AiSuggestsInvalidType_HeuristicTakesOver()
    {
        var result = await Run(ProcCsv(new() { "South", "North", "East" }, new() { 100m, 50m, 25m }), Ai("treemap"));

        Assert.Equal("pie", result.Blocks[0].TypeGraphic);
    }

    [Fact]
    public async Task ExecuteAsync_AiProvidesNoSuggestion_UsesHeuristic()
    {
        // ChartType null = no suggestion (e.g., LLM failed and hit text fallback)
        var result = await Run(ProcCsv(new() { "South", "North", "East" }, new() { 100m, 50m, 25m }), Ai(null, "text"));

        Assert.Equal("pie", result.Blocks[0].TypeGraphic);
        Assert.Equal("text", result.Blocks[0].InsightsIa[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_SingleCategory_AlwaysReturnsKpi()
    {
        // Even if AI requests "pie", 1 category cannot be rendered as a pie chart
        var result = await Run(ProcCsv(new() { "ILIKIA" }, new() { 100m }), Ai("pie"));

        Assert.Equal("kpi", result.Blocks[0].TypeGraphic);
    }

    // ---------- Batch C: ranking ----------

    [Fact]
    public async Task ExecuteAsync_Ranking_OrdersByValueWithIndividualShare()
    {
        var result = await Run(
            ProcCsv(new() { "South", "North", "East" }, new() { 50m, 100m, 25m }),
            Ai("bar"));

        var top = result.Blocks[0].Kpis.TopItems;

        Assert.Equal(3, top.Count);
        Assert.Equal("North", top[0].Label);   // highest value, not first along axis
        Assert.Equal(1, top[0].Rank);
        Assert.Equal("South", top[1].Label);
        Assert.Equal("East", top[2].Label);
    }
}