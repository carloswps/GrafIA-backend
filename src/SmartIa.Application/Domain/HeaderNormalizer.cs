using System.Globalization;
using System.Text;

namespace SmartIa.Application.Domain;

public static class HeaderNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sb = new StringBuilder(name.Length);

        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}