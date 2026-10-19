using SmartIa.Infrastructure.Sanitization;
using Xunit;

public class DataSanitizerServiceTests
{
    [Fact]
    public void SanitizeMetaData_DisallowedColumn_Discarded()
    {
        var svc = new DataSanitizerService();
        var raw = new Dictionary<string, List<string>>
        {
            ["Regiao"] = new() { "Sul" },
            ["CPF Cliente"] = new() { "123.456.789-00" }
        };

        var result = svc.SanitizeMetaData(raw);
        Assert.Contains("Regiao", result.Keys);
        Assert.DoesNotContain("CPF Cliente", result.Keys);
    }
    [Fact]
    public void SanitizeMetaData_DocEmail_Anonymized()
    {
        var svc = new DataSanitizerService();
        var raw = new Dictionary<string, List<string>>
        {
            ["Regiao"] = new() { "contato@teste.com", "123.456.789-00" }
        };

        var result = svc.SanitizeMetaData(raw);

        Assert.DoesNotContain("contato@teste.com", result["Regiao"]);
        Assert.Contains("[MASCARADO_EMAIL]", result["Regiao"][0]);
    }
}