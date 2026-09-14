using System.Text.RegularExpressions;

namespace SmartIa.Infrastructure.Files;

public static class TextCleaning
{
    public static string SanitizeText(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var clean = Regex.Replace(texto, @"[\r\n\t\0]", "").Trim();
        return clean.Replace("\uFFFD", "");
    }

    public static string CleanHeaderName(string rawHeader)
    {
        if (string.IsNullOrWhiteSpace(rawHeader)) return "Coluna";
        var clean = SanitizeText(rawHeader);
        return clean.Split(',')[0].Trim();
    }
}
