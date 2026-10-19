namespace SmartIa.Application.Domain;

public static class UnitInference
{

    private static readonly HashSet<string> Money =
        new[]
        {
            "Total", "TotalBruto", "Unit", "UnitBruto", "Venda Total Liq", "Venda Unit Liq",
            "Val IPI", "Val ICMS", "Vl IPI Unit", "VlICMSUnit", "Frete",
            "Dev Unit", "Dev Total", "Dev Unit 2", "Dev Total 2", "Valor Devolvido"
        }.Select(HeaderNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);

    public static string Infer(string columnY)
    {
        if (string.IsNullOrWhiteSpace(columnY)) return "number";

        if (columnY.Contains("(%)", StringComparison.Ordinal)) return "%";

        var normalized = HeaderNormalizer.Normalize(columnY);

        if (Money.Contains(normalized)) return "BRL";
        if (normalized.StartsWith("qtd", StringComparison.Ordinal)) return "un";
        if (normalized.Contains("percent", StringComparison.Ordinal)) return "%";
        return "number";
    }
}
