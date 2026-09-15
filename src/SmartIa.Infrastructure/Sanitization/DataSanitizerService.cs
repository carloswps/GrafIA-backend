using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Sanitization;

public partial class DataSanitizerService : IDataSanitizerService
{

    // whitelist: only allow these columns
    private static readonly HashSet<string> AllowedColumns = new(
    new[] {
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
    }.Select(HeaderNormalizer.Normalize), StringComparer.Ordinal);


    private static readonly string[] DatePrefixes = new[] { "Data", "Dt", "Emissao", "Validade" }.Select(HeaderNormalizer.Normalize).ToArray();

    private static readonly string[] PiiTerms =
        new[]
        {
        "nascimento", "nasc", "admissao", "demissao", "cpf", "cnpj", "rg",
        "carteira", "passaporte", "titulo", "eleitor", "pis", "pasep", "matricula",
        "funcionario", "cliente", "fornecedor", "contato", "pessoal", "pessoa",
        "email", "telefone", "celular", "fone", "whatsapp",
        "endereco", "logradouro", "cep", "rua", "complemento",
        "mae", "pai", "responsavel", "banco", "agencia", "conta"
    }.Select(HeaderNormalizer.Normalize).ToArray();


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
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        var normalized = HeaderNormalizer.Normalize(columnName);
        if (normalized.Length == 0) return false;
        if (AllowedColumns.Contains(normalized)) return true;
        return HasDatePrefix(normalized) && !LooksLikePii(normalized);
    }

    private static bool HasDatePrefix(string name) =>
            DatePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikePii(string name) =>
        PiiTerms.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));

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
