using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.GoogleSheets;

/// <summary>
/// Google Sheets 服務實作
/// </summary>
public class GoogleSheetService : IGoogleSheetService
{
    private readonly SheetsService _sheetsService;
    private readonly ILogger<GoogleSheetService> _logger;

    /// <summary>
    /// 初始化 <see cref="GoogleSheetService"/> 類別的新執行個體
    /// </summary>
    /// <param name="serviceAccountCredentialPath">服務帳戶憑證 JSON 檔案路徑</param>
    /// <param name="logger">日誌記錄器</param>
    public GoogleSheetService(string serviceAccountCredentialPath, ILogger<GoogleSheetService> logger)
    {
        _logger = logger;
        var credential = GoogleCredential.FromFile(serviceAccountCredentialPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ReleaseKit"
        });
    }

    /// <inheritdoc/>
    public async Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range)
    {
        _logger.LogInformation("讀取 Google Sheet 資料：spreadsheetId={SpreadsheetId}, sheetName={SheetName}, range={Range}",
            spreadsheetId, sheetName, range);

        var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!{range}");
        var response = await request.ExecuteAsync();

        _logger.LogInformation("Google Sheet 資料讀取完成，共 {Count} 列", response.Values?.Count ?? 0);

        return response.Values;
    }

    /// <inheritdoc/>
    public async Task<int> GetSheetIdAsync(string spreadsheetId, string sheetName)
    {
        _logger.LogInformation("取得工作表 ID：spreadsheetId={SpreadsheetId}, sheetName={SheetName}",
            spreadsheetId, sheetName);

        var request = _sheetsService.Spreadsheets.Get(spreadsheetId);
        var spreadsheet = await request.ExecuteAsync();

        var sheet = spreadsheet.Sheets
            .FirstOrDefault(s => s.Properties.Title == sheetName);

        if (sheet == null)
        {
            throw new InvalidOperationException(
                $"工作表 '{sheetName}' 在試算表 '{spreadsheetId}' 中不存在");
        }

        var sheetId = sheet.Properties.SheetId
            ?? throw new InvalidOperationException(
                $"工作表 '{sheetName}' 的 SheetId 為 null");

        _logger.LogInformation("取得工作表 ID 完成：sheetId={SheetId}", sheetId);

        return sheetId;
    }

    /// <inheritdoc/>
    public async Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex)
    {
        _logger.LogInformation("插入空白列：spreadsheetId={SpreadsheetId}, sheetId={SheetId}, rowIndex={RowIndex}",
            spreadsheetId, sheetId, rowIndex);

        var request = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new()
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
                }
            }
        };

        await _sheetsService.Spreadsheets.BatchUpdate(request, spreadsheetId).ExecuteAsync();

        _logger.LogInformation("插入空白列完成");
    }

    /// <inheritdoc/>
    public async Task UpdateCellsAsync(string spreadsheetId, string sheetName, IReadOnlyDictionary<string, string> updates)
    {
        _logger.LogInformation("批次更新儲存格：spreadsheetId={SpreadsheetId}, sheetName={SheetName}, count={Count}",
            spreadsheetId, sheetName, updates.Count);

        var valueRanges = updates.Select(kv => new ValueRange
        {
            Range = $"{sheetName}!{kv.Key}",
            Values = new List<IList<object>> { new List<object> { kv.Value } }
        }).ToList();

        var batchRequest = new BatchUpdateValuesRequest
        {
            ValueInputOption = "USER_ENTERED",
            Data = valueRanges
        };

        await _sheetsService.Spreadsheets.Values.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();

        _logger.LogInformation("批次更新儲存格完成");
    }
}
