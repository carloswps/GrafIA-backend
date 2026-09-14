using System.Globalization;
using System.Text.RegularExpressions;
using SmartIa.Application.Dtos;

namespace SmartIa.Infrastructure.Files;

public static class DatasetAnalyzer
{
    public static (List<string> Labels, List<decimal> Values) GroupTopValues(
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
            .OrderByDescending(x => x.Values)
            .Take(10)
            .ToList();

        return (groups.Select(x => x.Key).ToList(), groups.Select(x => x.Values).ToList());
    }

    public static DataSummaryDto BuildSummary(List<decimal> values, string columnX, string columnY)
    {
        var total = values.Sum();
        var media = values.Any() ? total / values.Count : 0;
        var max = values.Any() ? values.Max() : 0;
        var min = values.Any() ? values.Min() : 0;
        var top1 = total > 0 && values.Any() ? (double)(values[0] / total * 100) : 0;
        var top3 = total > 0 ? (double)(values.Take(3).Sum() / total * 100) : 0;

        return new DataSummaryDto(total, media, max, min, top1, top3, columnX, columnY);
    }

    public static bool IsGenericHeader(string h)
    {
        return string.IsNullOrWhiteSpace(h) ||
               Regex.IsMatch(h, @"^(Column|Coluna|Unnamed|F)\d*$", RegexOptions.IgnoreCase);
    }

    public static (string columnX, string columnY) ResolveColumns(List<Dictionary<string, string>> cleanRecords,
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

        var priorityNumericColumns = new[] { "Total", "TotalBruto", "Venda Total Liq", "Valor", "Valor Devolvido" };

        foreach (var col in priorityNumericColumns)
            if (cleanRecords.First().ContainsKey(col))
            {
                columnY = col;
                break;
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

            var type = "text";
            if (sampleValues.Take(10).All(v => decimal.TryParse(v, out _)))
                type = "numeric";
            else if (sampleValues.Take(10).All(v => DateTime.TryParse(v, out _)))
                type = "date";

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

    // ---------- Private Helpers ----------

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

    private static bool IsParsableDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var clean = Regex.Replace(input, @"[^\d,.-]", "").Trim();

        if (string.IsNullOrEmpty(clean)) return false;

        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out _))
            return true;

        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
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
}
