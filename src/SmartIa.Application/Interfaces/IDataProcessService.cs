using SmartIa.Application.Dtos;

namespace SmartIa.Application.Interfaces;

public interface IDataProcessService
{
    (List<string> Labels, List<decimal> Values, DataSummaryDto Summary) ProcessDataCsv(
        Stream csvStream, ParametersFilterDto filters);

    List<(string Title, List<string> Labels, List<decimal> Values, DataSummaryDto Summary)>
        ProcessDataExcelMulti(Stream excelStream, ParametersFilterDto filters);

    DatasetMetadataDto ExtractMetadata(List<Dictionary<string, string>> records);

    List<Dictionary<string, string>> GetRawRecordsFromCsv(Stream csvStream);

    List<Dictionary<string, string>> GetRawRecordsFromExcel(Stream excelStream);
}
