namespace smart_ia.Services.Interfaces;

public interface IDataSanitizerService
{
    Dictionary<string, List<string>> SanitizeMetaData(Dictionary<string, List<string>> rawMetaData);
}