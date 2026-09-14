namespace SmartIa.Application.Domain;


public static class GraphicTypeDetector
{
    public static string Detect(List<string> labels, List<decimal> values)
    {
        if (labels.All(l => DateTime.TryParse(l, out _))) return "line";
        if (labels.Count <= 5 && values.Sum() > 0) return "pie";
        return "bar";
    }
}
