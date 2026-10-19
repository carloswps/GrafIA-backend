using SmartIa.Application.Exceptions;

namespace SmartIa.Api.Validation;

public static class FileUploadValidator
{
    private static readonly string[] AllowedExtensions = [".csv", ".xlsx", ".xls"];

    public static void EnsureValid(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("O arquivo é obrigatório");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new UnsupportedFileTypeException($"Extensão '{extension}' não suportada. Permitidas: {string.Join(", ", AllowedExtensions)}");
    }
}
