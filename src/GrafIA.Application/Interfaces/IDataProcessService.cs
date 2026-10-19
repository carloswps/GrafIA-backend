using SmartIa.Application.Dtos;

namespace SmartIa.Application.Interfaces;

public interface IDataProcessService
{
    ProcessResultDto ProcessDataCsv(Stream csvStream, ParametersFilterDto filters);

    List<ProcessResultDto> ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters);

    DatasetMetadataDto ExtractMetadata(List<Dictionary<string, string>> records);

    List<Dictionary<string, string>> GetRawRecordsFromCsv(Stream csvStream);

    List<Dictionary<string, string>> GetRawRecordsFromExcel(Stream excelStream);

    List<(string TableName, List<Dictionary<string, string>> Records)> GetRawRecordsFromExcelAll(Stream excelStream);
}
