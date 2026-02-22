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

    private GoogleSheetService(SheetsService sheetsService, ILogger<GoogleSheetService> logger)
    {
        _sheetsService = sheetsService;
        _logger = logger;
    }

    /// <summary>
    /// 建立 <see cref="GoogleSheetService"/> 執行個體（使用 Service Account 認證）
    /// </summary>
    /// <param name="serviceAccountCredentialPath">Service Account JSON 金鑰檔路徑</param>
    /// <param name="logger">日誌記錄器</param>
    public static async Task<GoogleSheetService> CreateAsync(
        string serviceAccountCredentialPath,
        ILogger<GoogleSheetService> logger)
    {
        using var stream = File.OpenRead(serviceAccountCredentialPath);
        var serviceAccountCred = await CredentialFactory.FromStreamAsync<ServiceAccountCredential>(stream, CancellationToken.None);
        var credential = serviceAccountCred.ToGoogleCredential()
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        var sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ReleaseKit"
        });

        return new GoogleSheetService(sheetsService, logger);
    }

    /// <summary>
    /// 讀取指定範圍的資料
    /// </summary>
    public async Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range)
    {
        var fullRange = $"{sheetName}!{range}";
        _logger.LogInformation("讀取 Google Sheet 資料，範圍：{Range}", fullRange);

        var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, fullRange);
        var response = await request.ExecuteAsync();

        _logger.LogInformation("讀取完成，共 {RowCount} 列", response.Values?.Count ?? 0);
        return response.Values;
    }

    /// <summary>
    /// 透過工作表名稱取得工作表整數 ID
    /// </summary>
    public async Task<int> GetSheetIdAsync(string spreadsheetId, string sheetName)
    {
        _logger.LogInformation("查詢工作表 '{SheetName}' 的 ID", sheetName);

        var request = _sheetsService.Spreadsheets.Get(spreadsheetId);
        request.Fields = "sheets(properties(sheetId,title))";
        var spreadsheet = await request.ExecuteAsync();

        var sheet = spreadsheet.Sheets
            .FirstOrDefault(s => string.Equals(s.Properties.Title, sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheet == null)
        {
            throw new InvalidOperationException(
                $"找不到名稱為 '{sheetName}' 的工作表（試算表 ID：{spreadsheetId}）");
        }

        var sheetId = (int)(sheet.Properties.SheetId ?? 0);
        _logger.LogInformation("工作表 '{SheetName}' 的 ID 為 {SheetId}", sheetName, sheetId);
        return sheetId;
    }

    /// <summary>
    /// 在指定位置插入空白列
    /// </summary>
    public async Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex)
    {
        _logger.LogInformation("在 sheetId={SheetId} 的第 {RowIndex} 列插入空白列", sheetId, rowIndex);

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

    /// <summary>
    /// 批次更新多個儲存格值
    /// </summary>
    public async Task UpdateCellsAsync(string spreadsheetId, string sheetName, IReadOnlyDictionary<string, string> updates)
    {
        if (updates.Count == 0) return;

        _logger.LogInformation("批次更新 {Count} 個儲存格", updates.Count);

        var valueRanges = updates.Select(kv => new ValueRange
        {
            Range = $"{sheetName}!{kv.Key}",
            Values = new List<IList<object>> { new List<object> { kv.Value } }
        }).ToList();

        var batchRequest = new BatchUpdateValuesRequest
        {
            Data = valueRanges,
            ValueInputOption = "USER_ENTERED"
        };

        await _sheetsService.Spreadsheets.Values.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
        _logger.LogInformation("批次更新儲存格完成");
    }
}
