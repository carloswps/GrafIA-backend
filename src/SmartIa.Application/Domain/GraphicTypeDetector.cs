using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace SmartIa.Application.Domain;


public static class GraphicTypeDetector
{
    public static readonly IReadOnlySet<string> KnownTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pie", "bar", "line", "kpi", "funnel", "gauge", "pictorialBar" };

    public static string Detect(List<string> labels, List<decimal> values)
    {
        if (labels.Count <= 1) return "kpi";
        if (labels.All(l => DateTime.TryParse(l, out _))) return "line";

        if (values.Count > 0 && values.Sum() > 0)
        {
            var topShare = (double)(values.Max() / values.Sum());
            if (labels.Count <= 6 && topShare < 0.9) return "pie";
        }
        return "bar";
    }

    public static bool Allows(string? chartType, List<string> labels, List<decimal> values)
    {
        if (string.IsNullOrWhiteSpace(chartType)) return false;
        if (!KnownTypes.Contains(chartType)) return false;

        var isDate = labels.All(l => DateTime.TryParse(l, out _));

        if (labels.Count <= 1)
            return chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase) || chartType.Equals("gauge", StringComparison.OrdinalIgnoreCase);

        if (isDate)
            return chartType.Equals("line", StringComparison.OrdinalIgnoreCase) || chartType.Equals("bar", StringComparison.OrdinalIgnoreCase);

        if (chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase) || chartType.Equals("gauge", StringComparison.OrdinalIgnoreCase))
            return false;

        if (chartType.Equals("pie", StringComparison.OrdinalIgnoreCase))
        {
            if (values.Count > 6) return false;
            if (values.Count == 0 || values.Sum() <= 0) return false;
            if ((double)(values.Max() / values.Sum()) >= 0.9) return false;
        }

        if (chartType.Equals("funnel", StringComparison.OrdinalIgnoreCase))
            return labels.Count is >= 3 and <= 8;

        if (chartType.Equals("pictorialBar", StringComparison.OrdinalIgnoreCase))
            return labels.Count is >= 2 and <= 10;

        return true;
    }

    public static string Resolve(string? aiChartType, List<string> labels, List<decimal> values)
    {
        if (Allows(aiChartType, labels, values))
            return aiChartType!.ToLowerInvariant();

        return Detect(labels, values);
    }

    public static bool Allows(string? chartType, string columnType, int uniqueValuesCount)
    {
        if (string.IsNullOrWhiteSpace(chartType)) return false;
        if (!KnownTypes.Contains(chartType)) return false;

        var isDate = columnType.Equals("date", StringComparison.OrdinalIgnoreCase);

        if (uniqueValuesCount <= 1)
            return chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase);

        if (isDate)
            return chartType.Equals("line", StringComparison.OrdinalIgnoreCase)
                || chartType.Equals("bar", StringComparison.OrdinalIgnoreCase);

        if (chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase)) return false;

        if (chartType.Equals("pie", StringComparison.OrdinalIgnoreCase))
            return uniqueValuesCount <= 6;

        return true;
    }

    public static string Detect(string columnType, int uniqueValuesCont)
    {
        if (uniqueValuesCont <= 1) return "kpi";
        if (columnType.Equals("date", StringComparison.OrdinalIgnoreCase)) return "line";
        if (uniqueValuesCont <= 6) return "pie";
        return "bar";
    }

    public static string Resolve(string? aiChartType, string columnType, int uniqueValuesCount)
    {
        if (Allows(aiChartType, columnType, uniqueValuesCount))
        {
            return aiChartType!.ToLowerInvariant();
        }

        return Detect(columnType, uniqueValuesCount);
    }
}
