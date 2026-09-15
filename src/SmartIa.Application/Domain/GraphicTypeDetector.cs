namespace SmartIa.Application.Domain;


public static class GraphicTypeDetector
{
    public static readonly IReadOnlySet<string> KnownTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pie", "bar", "line", "kpi" };

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

        if (labels.Count <= 1) return chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase);

        if (labels.All(l => DateTime.TryParse(l, out _)))
            return chartType.Equals("line", StringComparison.OrdinalIgnoreCase)
                || chartType.Equals("bar", StringComparison.OrdinalIgnoreCase);

        if (chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase)) return false;

        if (chartType.Equals("pie", StringComparison.OrdinalIgnoreCase))
        {
            if (labels.Count > 6) return false;
            if (values.Count == 0 || values.Sum() <= 0) return false;
            if ((double)(values.Max() / values.Sum()) >= 0.9) return false;
        }

        return true;
    }

    public static string Resolve(string? aiChartType, List<string> labels, List<decimal> values)
    {
        if (Allows(aiChartType, labels, values))
            return aiChartType!.ToLowerInvariant();

        return Detect(labels, values);
    }
}
