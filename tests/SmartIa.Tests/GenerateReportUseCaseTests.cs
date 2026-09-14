using Moq;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;
using SmartIa.Application.UseCases;
using Xunit;

public class GenerateReportUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_Csv_GeneratesABlock()
    {
        var proc = new Mock<IDataProcessService>();
        proc.Setup(p => p.ProcessDataCsv(It.IsAny<Stream>(), It.IsAny<ParametersFilterDto>()))
            .Returns((new List<string> { "Sul" }, new List<decimal> { 100m },
                new DataSummaryDto(100, 100, 100, 100, 100, 100, "Regiao", "Total")));

        var ai = new Mock<IAiInsightService>();
        ai.Setup(a => a.GenerateInsightAsync(It.IsAny<List<string>>(), It.IsAny<List<decimal>>(), It.IsAny<DataSummaryDto>()))
            .ReturnsAsync("insight");

        var useCase = new GenerateReportUseCase(proc.Object, ai.Object);

        var result = await useCase.ExecuteAsync(new MemoryStream(), "vendas.csv",
            new ParametersFilterDto(null, null, null, null));

        Assert.Single(result.Blocks);
    }
}