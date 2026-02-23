using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.GoogleSheets;

/// <summary>
/// Google Sheets 服務實作，使用 Google.Apis.Sheets.v4 與 Service Account 認證
/// </summary>
public class GoogleSheetService : IGoogleSheetService
{
    private readonly SheetsService _sheetsService;
    private readonly ILogger<GoogleSheetService> _logger;

    /// <summary>
    /// 初始化 <see cref="GoogleSheetService"/> 類別的新執行個體
    /// </summary>
    /// <param name="options">Google Sheet 配置選項</param>
    /// <param name="logger">日誌記錄器</param>
    public GoogleSheetService(
        IOptions<GoogleSheetOptions> options,
        ILogger<GoogleSheetService> logger)
    {
        _logger = logger;

        var credentialPath = options.Value.ServiceAccountCredentialPath;
        if (string.IsNullOrWhiteSpace(credentialPath))
        {
            throw new InvalidOperationException("缺少必要的組態鍵: GoogleSheet:ServiceAccountCredentialPath");
        }

        var credential = CredentialFactory
            .FromFile<ServiceAccountCredential>(credentialPath)
            .ToGoogleCredential()
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ReleaseKit"
        });
    }

    /// <summary>
    /// 讀取指定範圍的 Google Sheet 資料
    /// </summary>
    public async Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range)
    {
        var fullRange = $"{sheetName}!{range}";
        _logger.LogInformation("讀取 Google Sheet 資料：{Range}", fullRange);

        var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, fullRange);
        var response = await request.ExecuteAsync();

        _logger.LogInformation("讀取完成，共 {RowCount} 列", response.Values?.Count ?? 0);
        return response.Values;
    }

    /// <summary>
    /// 在指定位置插入空白列
    /// </summary>
    public async Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex)
    {
        _logger.LogInformation("在 Sheet {SheetId} 的 Row {RowIndex} 插入空白列", sheetId, rowIndex);

        var insertRequest = new Request
        {
            InsertDimension = new InsertDimensionRequest
            {
                Range = new DimensionRange
                {
                    SheetId = sheetId,
                    Dimension = "ROWS",
                    StartIndex = rowIndex,
                    EndIndex = rowIndex + 1
                },
                InheritFromBefore = false
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { insertRequest }
        };

        await _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
    }

    /// <summary>
    /// 批次更新多個儲存格值（支援文字與 HYPERLINK 公式）
    /// </summary>
    public async Task UpdateCellsAsync(string spreadsheetId, string sheetName, IDictionary<string, object> updates)
    {
        _logger.LogInformation("批次更新 {Count} 個儲存格", updates.Count);

        var data = updates.Select(u => new ValueRange
        {
            Range = $"{sheetName}!{u.Key}",
            Values = new List<IList<object>> { new List<object> { u.Value } }
        }).ToList();

        var batchUpdateRequest = new BatchUpdateValuesRequest
        {
            ValueInputOption = "USER_ENTERED",
            Data = data
        };

        await _sheetsService.Spreadsheets.Values.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
    }
}
