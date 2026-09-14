using System.Text.RegularExpressions;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Sanitization;

public partial class DataSanitizerService : IDataSanitizerService
{
    // whitelist: only allow these columns
    private static readonly HashSet<string> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Empresa", "Filial", "Nome Filial", "Cod Categoria", "Categoria",
        "Cod Produto", "Produto", "Regiao Cod", "Regiao", "UF", "Estado",
        "Cidade", "Bairro", "Natureza Operacao", "Desc Natureza", "Classificacao",
        "Canal", "Sistema", "Evento", "Descricao Evento", "Evento2",
        "Condicao Pagto", "Forma Pagto", "Especialidade", "Especialidade Desc",
        "Grupo", "Nome Grupo", "TES", "CFOP",

        "Qtd", "Unit", "Total", "Val IPI", "Val ICMS", "Vl IPI Unit", "VlICMSUnit",
        "UnitBruto", "Frete", "TotalBruto", "Qtd Dev", "Dev Unit", "Dev Total",
        "Dev Unit 2", "Dev Total 2", "Qtd Liq", "Venda Unit Liq", "Venda Total Liq",
        "Qtd Devolvida", "Valor Devolvido"
    };

    private static readonly Regex CpfCnpjRegex = CnpjCpfRegex();

    private static readonly Regex EmailRegex = RegexEmail();

    public Dictionary<string, List<string>> SanitizeMetaData(Dictionary<string, List<string>> rawMetaData)
    {
        var sanitizedMetadata = new Dictionary<string, List<string>>();
        foreach (var (columnName, sampleValues) in rawMetaData)
        {
            if (!IsAllowedColumn(columnName))
                continue;

            var cleanSamples = sampleValues.Select(AnonymizeSampleValue).Where(val => !string.IsNullOrWhiteSpace(val))
                .Distinct().ToList();

            sanitizedMetadata[columnName] = cleanSamples;
        }

        return sanitizedMetadata;
    }

    private bool IsAllowedColumn(string columnName)
    {
        if (AllowedColumns.Contains(columnName)) return true;

        if (columnName.StartsWith("Data", StringComparison.OrdinalIgnoreCase) ||
            columnName.StartsWith("Dt ", StringComparison.OrdinalIgnoreCase) ||
            columnName.StartsWith("Emissao", StringComparison.OrdinalIgnoreCase) ||
            columnName.StartsWith("Validade", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private string AnonymizeSampleValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var sanitized = CpfCnpjRegex.Replace(value, "[MASCARADO_DOC]");
        sanitized = EmailRegex.Replace(sanitized, "[MASCARADO_EMAIL]");

        return sanitized;
    }

    [GeneratedRegex(@"\d{3}\.?\d{3}\.?\d{3}-?\d{2}|\d{2}\.?\d{3}\.?\d{3}/\d{4}-?\d{2}", RegexOptions.Compiled)]
    private static partial Regex CnpjCpfRegex();
    [GeneratedRegex(@"[^@\s]+@[^@\s]+\.[^@\s]+", RegexOptions.Compiled)]
    private static partial Regex RegexEmail();
}
