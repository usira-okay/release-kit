using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
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

    /// <summary>
    /// 初始化 <see cref="GoogleSheetService"/> 類別的新執行個體
    /// </summary>
    /// <param name="options">Google Sheet 設定選項</param>
    public GoogleSheetService(IOptions<GoogleSheetOptions> options)
    {
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
    internal GoogleSheetService(SheetsService sheetsService)
    {
        _sheetsService = sheetsService;
    }

    /// <summary>
    /// 透過工作表名稱取得 SheetId
    /// </summary>
    public async Task<int?> GetSheetIdByNameAsync(string spreadsheetId, string sheetName)
    {
        var spreadsheet = await _sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
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
        var response = await request.ExecuteAsync();
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

        await _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
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
        await updateRequest.ExecuteAsync();
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

        await _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
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

        await _sheetsService.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
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

        await _sheetsService.Spreadsheets.Values
            .BatchUpdate(batchUpdateRequest, spreadsheetId)
            .ExecuteAsync();
    }
}
