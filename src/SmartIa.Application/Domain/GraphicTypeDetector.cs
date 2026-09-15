namespace SmartIa.Application.Domain;


public static class GraphicTypeDetector
{
    private static readonly HashSet<string> KnownTypes =
        new(StringComparer.OrdinalIgnoreCase) { "pie", "bar", "line", "kpi" };

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

    /// <summary>
    /// Regras que a IA NAO pode sobrepor. A heuristica sabe coisas que a IA nao ve:
    /// a cardinalidade e a distribuicao reais dos dados agregados.
    /// </summary>
    public static bool Allows(string? chartType, List<string> labels, List<decimal> values)
    {
        if (string.IsNullOrWhiteSpace(chartType)) return false;
        if (!KnownTypes.Contains(chartType)) return false;

        // 1 categoria nunca e pizza/barra/linha: e um KPI
        if (labels.Count <= 1) return chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase);

        // eixo temporal so aceita linha (ou barra)
        if (labels.All(l => DateTime.TryParse(l, out _)))
            return chartType.Equals("line", StringComparison.OrdinalIgnoreCase)
                || chartType.Equals("bar", StringComparison.OrdinalIgnoreCase);

        if (chartType.Equals("kpi", StringComparison.OrdinalIgnoreCase)) return false;

        // pizza so com poucas fatias E distribuicao real (sem 1 fatia dominante)
        if (chartType.Equals("pie", StringComparison.OrdinalIgnoreCase))
        {
            if (labels.Count > 6) return false;
            if (values.Count == 0 || values.Sum() <= 0) return false;
            if ((double)(values.Max() / values.Sum()) >= 0.9) return false;
        }

        return true;
    }

    /// <summary>A IA opina; a heuristica veta. Sem opiniao valida, cai no Detect.</summary>
    public static string Resolve(string? aiChartType, List<string> labels, List<decimal> values)
    {
        if (Allows(aiChartType, labels, values))
            return aiChartType!.ToLowerInvariant();

        return Detect(labels, values);
    }
}
