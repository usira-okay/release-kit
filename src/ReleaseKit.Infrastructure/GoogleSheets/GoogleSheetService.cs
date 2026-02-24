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
/// Google Sheet 資料存取服務實作
/// </summary>
public class GoogleSheetService : IGoogleSheetService
{
    private readonly SheetsService _sheetsService;
    private readonly ILogger<GoogleSheetService> _logger;

    /// <summary>
    /// Rate limit 每次等待時間
    /// </summary>
    protected virtual TimeSpan RateLimitDelay => TimeSpan.FromMinutes(1);

    /// <summary>
    /// Rate limit 最大重試次數
    /// </summary>
    private const int MaxRateLimitRetries = 3;

    /// <summary>
    /// 初始化 <see cref="GoogleSheetService"/> 類別的新執行個體
    /// </summary>
    /// <param name="options">Google Sheet 設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public GoogleSheetService(IOptions<GoogleSheetOptions> options, ILogger<GoogleSheetService> logger)
    {
        _logger = logger;

        var credential = GoogleCredential
            .FromFile(options.Value.ServiceAccountCredentialPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ReleaseKit"
        });
    }

    /// <summary>
    /// 供測試使用的建構子，允許注入 SheetsService
    /// </summary>
    internal GoogleSheetService(SheetsService sheetsService, ILogger<GoogleSheetService> logger)
    {
        _sheetsService = sheetsService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Google Sheets API 呼叫，並在達到請求限制（429）時自動等待重試，最多重試 3 次，每次等待 1 分鐘
    /// </summary>
    private async Task<T> ExecuteWithRateLimitAsync<T>(Func<Task<T>> action)
    {
        var retryCount = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 429)
            {
                retryCount++;
                if (retryCount > MaxRateLimitRetries)
                    throw;
                _logger.LogWarning(
                    "Google Sheets API 達到請求限制，等待 {Delay} 後重試（第 {Retry}/{Max} 次）",
                    RateLimitDelay, retryCount, MaxRateLimitRetries);
                await Task.Delay(RateLimitDelay);
            }
        }
    }

    /// <summary>
    /// 執行無回傳值的 Google Sheets API 呼叫，並在達到請求限制（429）時自動等待重試，最多重試 3 次，每次等待 1 分鐘
    /// </summary>
    private async Task ExecuteWithRateLimitAsync(Func<Task> action)
    {
        var retryCount = 0;
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 429)
            {
                retryCount++;
                if (retryCount > MaxRateLimitRetries)
                    throw;
                _logger.LogWarning(
                    "Google Sheets API 達到請求限制，等待 {Delay} 後重試（第 {Retry}/{Max} 次）",
                    RateLimitDelay, retryCount, MaxRateLimitRetries);
                await Task.Delay(RateLimitDelay);
            }
        }
    }

    /// <summary>
    /// 透過工作表名稱取得 SheetId
    /// </summary>
    public async Task<int?> GetSheetIdByNameAsync(string spreadsheetId, string sheetName)
    {
        var spreadsheet = await ExecuteWithRateLimitAsync(
            () => _sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync());
        var sheet = spreadsheet.Sheets?.FirstOrDefault(
            s => string.Equals(s.Properties?.Title, sheetName, StringComparison.OrdinalIgnoreCase));

        return sheet?.Properties?.SheetId;
    }

    /// <summary>
    /// 讀取指定範圍的儲存格資料
    /// </summary>
    public async Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string range)
    {
        var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
        var response = await ExecuteWithRateLimitAsync(() => request.ExecuteAsync());
        return response.Values;
    }

    /// <summary>
    /// 讀取指定範圍的儲存格資料（含公式原文，用於保留 HYPERLINK 等公式）
    /// </summary>
    public async Task<IList<IList<object>>?> GetSheetDataWithFormulasAsync(string spreadsheetId, string range)
    {
        var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
        request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMULA;
        var response = await ExecuteWithRateLimitAsync(() => request.ExecuteAsync());
        return response.Values;
    }

    /// <summary>
    /// 在指定位置批次插入空白列
    /// </summary>
    public async Task InsertRowsAsync(string spreadsheetId, int sheetId, int rowIndex, int count)
    {
        var insertRequest = new Request
        {
            InsertDimension = new InsertDimensionRequest
            {
                Range = new DimensionRange
                {
                    SheetId = sheetId,
                    Dimension = "ROWS",
                    StartIndex = rowIndex,
                    EndIndex = rowIndex + count
                },
                InheritFromBefore = false
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { insertRequest }
        };

        await ExecuteWithRateLimitAsync(
            () => _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync());
    }

    /// <summary>
    /// 批次更新指定範圍的儲存格值（純文字）
    /// </summary>
    public async Task UpdateCellsAsync(string spreadsheetId, string range, IList<IList<object>> values)
    {
        var valueRange = new ValueRange
        {
            Range = range,
            Values = values
        };

        var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await ExecuteWithRateLimitAsync(() => updateRequest.ExecuteAsync());
    }

    /// <summary>
    /// 更新單一儲存格並設定超連結（使用 HYPERLINK 公式）
    /// </summary>
    public async Task UpdateCellWithHyperlinkAsync(
        string spreadsheetId, int sheetId, int rowIndex, int columnIndex, string displayText, string url)
    {
        var updateCellsRequest = new Request
        {
            UpdateCells = new UpdateCellsRequest
            {
                Rows = new List<RowData>
                {
                    new()
                    {
                        Values = new List<CellData>
                        {
                            new()
                            {
                                UserEnteredValue = new ExtendedValue
                                {
                                    FormulaValue = $"=HYPERLINK(\"{url.Replace("\"", "\"\"")}\",\"{displayText.Replace("\"", "\"\"")}\")"
                                }
                            }
                        }
                    }
                },
                Start = new GridCoordinate
                {
                    SheetId = sheetId,
                    RowIndex = rowIndex,
                    ColumnIndex = columnIndex
                },
                Fields = "userEnteredValue"
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { updateCellsRequest }
        };

        await ExecuteWithRateLimitAsync(
            () => _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync());
    }

    /// <summary>
    /// 對指定範圍依指定欄位排序
    /// </summary>
    public async Task SortRangeAsync(
        string spreadsheetId, int sheetId, int startRowIndex, int endRowIndex,
        IList<(int ColumnIndex, bool Ascending)> sortSpecs)
    {
        var sortRequest = new Request
        {
            SortRange = new SortRangeRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = startRowIndex,
                    EndRowIndex = endRowIndex
                },
                SortSpecs = sortSpecs.Select(s => new SortSpec
                {
                    DimensionIndex = s.ColumnIndex,
                    SortOrder = s.Ascending ? "ASCENDING" : "DESCENDING"
                }).ToList()
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { sortRequest }
        };

        await ExecuteWithRateLimitAsync(
            () => _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync());
    }

    /// <summary>
    /// 批次更新多個儲存格範圍
    /// </summary>
    public async Task BatchUpdateCellsAsync(string spreadsheetId, IList<(string Range, IList<IList<object>> Values)> updates)
    {
        var data = updates.Select(u => new ValueRange
        {
            Range = u.Range,
            Values = u.Values
        }).ToList();

        var batchUpdateRequest = new BatchUpdateValuesRequest
        {
            Data = data,
            ValueInputOption = "USER_ENTERED"
        };

        await ExecuteWithRateLimitAsync(
            () => _sheetsService.Spreadsheets.Values
                .BatchUpdate(batchUpdateRequest, spreadsheetId)
                .ExecuteAsync());
    }
}
