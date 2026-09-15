using System.Globalization;
using System.Text.RegularExpressions;
using SmartIa.Application.Domain;
using SmartIa.Application.Dtos;

namespace SmartIa.Infrastructure.Files;

public static class DatasetAnalyzer
{
    private const int MaxGroups = 10;

    private static readonly string[] PriorityMetricNames =
        { "Total", "TotalBruto", "Venda Total Liq", "Valor", "Valor Devolvido" };

    private static readonly HashSet<string> KnowDimensions =
        new[]
        {
            "Regiao", "UF", "Estado", "Cidade", "Bairro", "Categoria", "Produto",
            "Grupo", "Nome Grupo", "Canal", "Sistema", "Especialidade", "Natureza Operacao"
        }.Select(HeaderNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);

    public static (List<string> Labels, List<decimal> Values, int TotalGroups) GroupTopValues(
        List<Dictionary<string, string>> cleanRecords, string columnX, string columnY)
    {
        var groups = cleanRecords
            .Where(r => r.ContainsKey(columnX) && r.ContainsKey(columnY))
            .GroupBy(r => TextCleaning.SanitizeText(r[columnX]))
            .Select(g => new
            {
                Key = g.Key,
                Values = g.Sum(r => ConvertToDecimal(r[columnY]))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !x.Key.Contains(",") && x.Values > 0)
            .ToList();

        var totalGroups = groups.Count;
        if (totalGroups == 0)
            return (new List<string>(), new List<decimal>(), 0);

        if (IsDateDimension(groups.Select(g => g.Key)))
        {
            var chrono = groups
                .Select(g => (g.Key, g.Values, Date: ParseDate(g.Key)))
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date!.Value)
                .Take(MaxGroups)
                .OrderBy(x => x.Date!.Value)
                .ToList();

            return (chrono.Select(x => x.Key).ToList(),
                    chrono.Select(x => x.Values).ToList(),
                    totalGroups);
        }

        var top = groups.OrderByDescending(x => x.Values).Take(MaxGroups).ToList();
        return (top.Select(x => x.Key).ToList(), top.Select(x => x.Values).ToList(), totalGroups);
    }

    public static DataSummaryDto BuildSummary(
        List<decimal> values, int totalGroups, string columnX, string columnY)
    {
        var total = values.Sum();
        var media = values.Any() ? total / values.Count : 0;
        var max = values.Any() ? values.Max() : 0;
        var min = values.Any() ? values.Min() : 0;

        var top1 = total > 0 && values.Any() ? (double)(values.Max() / total * 100) : 0;
        var top3 = total > 0
            ? (double)(values.OrderByDescending(v => v).Take(3).Sum() / total * 100)
            : 0;

        return new DataSummaryDto(
            total, media, max, min, top1, top3, columnX, columnY,
            GroupsShown: values.Count,
            TotalGroups: totalGroups,
            IsTruncated: totalGroups > values.Count);
    }

    public static bool IsGenericHeader(string h)
    {
        return string.IsNullOrWhiteSpace(h) ||
               Regex.IsMatch(h, @"^(Column|Coluna|Unnamed|F)\d*$", RegexOptions.IgnoreCase);
    }

    public static (string columnX, string columnY) ResolveColumns(
        List<Dictionary<string, string>> cleanRecords,
        ParametersFilterDto filters, string firstKey, string secondKey)
    {
        var columnX = TextCleaning.SanitizeText(filters.ColumnX ?? firstKey);
        var requestedY = string.IsNullOrWhiteSpace(filters.ColumnY) ? null : TextCleaning.SanitizeText(filters.ColumnY);

        var columnY = requestedY ?? secondKey;
        if (!cleanRecords.First().ContainsKey(columnY))
            columnY = secondKey;

        if (requestedY == null || !IsNumericColumn(cleanRecords, columnY))
        {
            var detected = DetectNumericColumn(cleanRecords, columnX);
            if (!string.IsNullOrEmpty(detected))
                columnY = detected;
        }

        if (string.Equals(columnX, columnY, StringComparison.OrdinalIgnoreCase))
        {
            var alternativeX = cleanRecords.First().Keys
                .FirstOrDefault(k => !string.Equals(k, columnY, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(alternativeX))
                columnX = alternativeX;
        }

        var priorityMetric = FindMetricColumn(cleanRecords.First().Keys, cleanRecords);
        if (priorityMetric is not null) columnY = priorityMetric;

        var xCardinality = cleanRecords.Select(r => r.GetValueOrDefault(columnX, ""))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

        if (xCardinality <= 1)
        {
            var better = PickBestDimension(cleanRecords, columnY);
            if (!string.Equals(better, columnY, StringComparison.OrdinalIgnoreCase))
                columnX = better;
        }

        return (columnX, columnY);
    }

    public static DatasetMetadataDto ExtractMetadata(List<Dictionary<string, string>> records)
    {
        var metadata = new DatasetMetadataDto
        {
            TotalRows = records.Count
        };

        if (!records.Any()) return metadata;

        var keys = records.First().Keys;

        foreach (var key in keys)
        {
            var sampleValues = records.Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            var uniqueCount = sampleValues.Distinct().Count();
            var sample = sampleValues.Take(10).ToList();

            var type = "text";
            if (sample.Count > 0 && sample.All(v => TryParseDate(v, out _)))
                type = "date";
            else if (sample.Count > 0 && sample.All(IsParsableDecimal))
                type = "numeric";

            metadata.Columns.Add(new ColumnSummaryDto
            {
                Name = key,
                Type = type,
                UniqueValuesCount = uniqueCount,
                SampleValues = sampleValues.Take(3).ToList()
            });
        }

        return metadata;
    }

    public static string PickBestDimension(List<Dictionary<string, string>> records, string exclude)
    {
        var best = exclude;
        var bestScore = -1.0;

        foreach (var key in records.First().Keys)
        {
            if (string.Equals(key, exclude, StringComparison.OrdinalIgnoreCase)) continue;

            if (IsNumericColumn(records, key)) continue;

            var distinct = records.Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();

            double score = distinct switch
            {
                <= 1 => 0,
                <= 15 => distinct,
                <= 50 => 15 - (distinct - 15) * 0.1,
                _ => 1
            };

            if (score <= 0) continue;
            if (KnowDimensions.Contains(HeaderNormalizer.Normalize(key))) score += 5;
            if (score > bestScore) { bestScore = score; best = key; }
        }
        return best;
    }

    // ---------- Private Helpers ----------
    private static string? FindMetricColumn(
        IEnumerable<string> header, List<Dictionary<string, string>> records)
    {
        foreach (var wanted in PriorityMetricNames.Select(HeaderNormalizer.Normalize))
        {
            var actual = header.FirstOrDefault(k => HeaderNormalizer.Normalize(k) == wanted);
            if (actual is not null && IsNumericColumn(records, actual))
                return actual;
        }
        return null;
    }

    private static string? DetectNumericColumn(List<Dictionary<string, string>> cleanRecords, string excludeColumn)
    {
        string? best = null;
        double bestRatio = 0;

        foreach (var key in cleanRecords.First().Keys)
        {
            if (string.Equals(key, excludeColumn, StringComparison.OrdinalIgnoreCase))
                continue;

            var sample = cleanRecords.Take(20).Select(r => r.GetValueOrDefault(key, ""))
                .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (!sample.Any())
                continue;

            var ratio = (double)sample.Count(IsParsableDecimal) / sample.Count;
            if (ratio >= 0.5)
                return key;

            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                best = key;
            }
        }

        return bestRatio > 0 ? best : null;
    }

    private static bool IsNumericColumn(List<Dictionary<string, string>> cleanRecords, string column)
    {
        var sample = cleanRecords.Take(20).Select(r => r.GetValueOrDefault(column, ""))
            .Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (!sample.Any())
            return false;

        return (double)sample.Count(IsParsableDecimal) / sample.Count >= 0.5;
    }

    private static decimal ConvertToDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0m;

        var clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();

        if (string.IsNullOrEmpty(clean)) return 0m;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var valPt))
            return valPt;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var valInv))
            return valInv;

        return 0m;
    }

    private static bool TryParseDate(string input, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (!input.Contains('/') && !input.Contains('-') && !input.Contains(':')) return false;

        return DateTime.TryParse(input, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out value);
    }

    private static DateTime? ParseDate(string input) => TryParseDate(input, out var d) ? d : null;

    private static bool IsParsableDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (TryParseDate(input, out _)) return false;

        var clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();
        if (string.IsNullOrWhiteSpace(clean)) return false;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out _)) return true;
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsDateDimension(IEnumerable<string> keys)
    {
        var list = keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        return list.Count > 0 && list.Count(k => ParseDate(k).HasValue) >= list.Count * 0.9;
    }
}
