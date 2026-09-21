using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;
using SmartIa.Application.Interfaces;

namespace SmartIa.Infrastructure.Sanitization;

public enum SensitivityLevel
{
    Public,
    Protected,
    Confidential
}

public partial class DataSanitizerService : IDataSanitizerService
{
    private static readonly string[] StrictPiiTerms = [
        "cpf", "cnpj", "rg", "passport", "passaporte", "cnh",
        "email", "mail", "telefone", "celular", "phone", "whatsapp",
        "senha", "password", "token", "hash", "secret",
        "cartao", "card", "cvv", "banco", "agencia"
    ];

    private static readonly string[] ContextualPiiTerms = ["conta"];
    private static readonly Regex CpfCnpjRegex = CnpjCpfRegex();
    private static readonly Regex EmailRegex = RegexEmail();

    private readonly IDictionary<string, SensitivityLevel> _columnPolicyOverride;

    public DataSanitizerService(IDictionary<string, SensitivityLevel>? columnPolicyOverride = null)
    {
        _columnPolicyOverride = columnPolicyOverride ?? new Dictionary<string, SensitivityLevel>(StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, List<string>> SanitizeMetaData(Dictionary<string, List<string>> rawMetaData)
    {
        var sanitizedMetadata = new Dictionary<string, List<string>>();
        foreach (var (columnName, sampleValues) in rawMetaData)
        {
            if (string.IsNullOrWhiteSpace(columnName) || sampleValues is null) continue;

            var sensitivity = EvaluateColumnSensitivity(columnName, sampleValues);

            if (sensitivity == SensitivityLevel.Confidential)
                continue;

            var cleanSamples = sampleValues.Select(AnonymizeSampleValue).Where(val => !string.IsNullOrWhiteSpace(val))
                .Distinct().ToList();

            sanitizedMetadata[columnName] = cleanSamples;
        }

        return sanitizedMetadata;
    }

    private SensitivityLevel EvaluateColumnSensitivity(string columnName, List<string> sampleValue)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return SensitivityLevel.Confidential;

        var normalizedName = HeaderNormalizer.Normalize(columnName);

        if (_columnPolicyOverride.TryGetValue(normalizedName, out var policy))
            return policy;

        if (ContainsPiiTerm(columnName))
            return SensitivityLevel.Confidential;

        if (HasHighPiiContentDensity(sampleValue))
            return SensitivityLevel.Protected;

        return SensitivityLevel.Public;
    }

    private static IEnumerable<string> Tokenize(string columnName) => Regex.Split(columnName, @"[^\p{L}\p{Nd}]+").Select(HeaderNormalizer.Normalize)
         .Where(t => t.Length > 0);

    private static bool ContainsPiiTerm(string columnName)
    {
        var tokens = Tokenize(columnName).ToList();

        if (StrictPiiTerms.Concat(ContextualPiiTerms).Any(term => tokens.Contains(term, StringComparer.Ordinal))) return true;

        var joined = string.Concat(tokens);
        return StrictPiiTerms.Any(term => term.Length >= 5 && joined.Contains(term, StringComparison.Ordinal));
    }

    private static bool HasHighPiiContentDensity(List<string> sampleValues)
    {
        var sampleToInspect = sampleValues.Where(s => !string.IsNullOrWhiteSpace(s)).Take(50).ToList();
        if (sampleValues.Count == 0) return false;

        if (sampleToInspect.Count == 0) return false;

        var piiMatches = sampleToInspect.Count(s => CpfCnpjRegex.IsMatch(s) || EmailRegex.IsMatch(s));

        return (double)piiMatches / sampleToInspect.Count > 0.3;
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
