using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 更新 Google Sheets 資訊任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取 ReleaseData:Consolidated 的整合資料，批次新增/更新至 Google Sheet，
/// 並在每個 Project 區塊內依 TeamColumn、Authors、FeatureColumn、UniqueKey 排序。
/// </remarks>
public class UpdateGoogleSheetsTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IGoogleSheetService _googleSheetService;
    private readonly IOptions<GoogleSheetOptions> _options;
    private readonly ILogger<UpdateGoogleSheetsTask> _logger;

    /// <summary>
    /// 初始化 <see cref="UpdateGoogleSheetsTask"/> 類別的新執行個體
    /// </summary>
    public UpdateGoogleSheetsTask(
        IRedisService redisService,
        IGoogleSheetService googleSheetService,
        IOptions<GoogleSheetOptions> options,
        ILogger<UpdateGoogleSheetsTask> logger)
    {
        _redisService = redisService;
        _googleSheetService = googleSheetService;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行更新 Google Sheets 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始更新 Google Sheets");

        var opts = _options.Value;

        // 4.3 驗證 ColumnMapping
        ValidateColumnMapping(opts.ColumnMapping);

        // 4.2 從 Redis 讀取整合資料
        var consolidatedJson = await _redisService.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
        if (string.IsNullOrEmpty(consolidatedJson))
        {
            _logger.LogWarning("Redis 中無整合資料：{Hash}:{Field}", RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
            return;
        }

        var consolidatedResult = consolidatedJson.ToTypedObject<ConsolidatedReleaseResult>();
        if (consolidatedResult == null || consolidatedResult.Projects.Count == 0)
        {
            _logger.LogWarning("整合資料為空，無需同步至 Google Sheet");
            return;
        }

        // 4.4 讀取 Google Sheet A:Z 資料
        IList<IList<object>>? sheetData;
        int sheetId;
        try
        {
            sheetData = await _googleSheetService.GetSheetDataAsync(opts.SpreadsheetId, opts.SheetName, "A:Z");
            sheetId = await _googleSheetService.GetSheetIdAsync(opts.SpreadsheetId, opts.SheetName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "讀取 Google Sheet 失敗，略過本次同步");
            return;
        }

        if (sheetData == null)
        {
            sheetData = new List<IList<object>>();
        }

        var colMap = opts.ColumnMapping;

        // 4.5 解析 RepositoryNameColumn — 取得所有非空值的 row index 與 ProjectName
        var repoRows = ParseRepositoryRows(sheetData, colMap.RepositoryNameColumn);

        // 4.6 解析 UniqueKeyColumn — 取得所有既有 UK 值與 row index 的映射
        var existingUkMap = ParseUniqueKeyRows(sheetData, colMap.UniqueKeyColumn);

        // 同步每個 Project 的資料
        foreach (var (projectName, entries) in consolidatedResult.Projects)
        {
            await SyncProjectAsync(projectName, entries, sheetData, repoRows, existingUkMap, opts, sheetId);
        }

        _logger.LogInformation("更新 Google Sheets 完成");
    }

    /// <summary>
    /// 驗證 ColumnMapping 所有欄位是否均為 A-Z 單字母
    /// </summary>
    private static void ValidateColumnMapping(ColumnMappingOptions colMap)
    {
        ValidateColumn(colMap.RepositoryNameColumn, nameof(colMap.RepositoryNameColumn));
        ValidateColumn(colMap.FeatureColumn, nameof(colMap.FeatureColumn));
        ValidateColumn(colMap.TeamColumn, nameof(colMap.TeamColumn));
        ValidateColumn(colMap.AuthorsColumn, nameof(colMap.AuthorsColumn));
        ValidateColumn(colMap.PullRequestUrlsColumn, nameof(colMap.PullRequestUrlsColumn));
        ValidateColumn(colMap.UniqueKeyColumn, nameof(colMap.UniqueKeyColumn));
        ValidateColumn(colMap.AutoSyncColumn, nameof(colMap.AutoSyncColumn));
    }

    private static void ValidateColumn(string value, string columnName)
    {
        var normalized = value?.ToUpperInvariant() ?? "";
        if (normalized.Length != 1 || normalized[0] < 'A' || normalized[0] > 'Z')
        {
            throw new InvalidOperationException(
                $"ColumnMapping 欄位設定無效：{columnName} = \"{value}\"，必須為 A-Z 的單字母");
        }
    }

    /// <summary>
    /// 解析 RepositoryNameColumn — 回傳 (row index 0-based, projectName) 清單（依出現順序）
    /// </summary>
    private static List<(int RowIndex, string ProjectName)> ParseRepositoryRows(
        IList<IList<object>> sheetData,
        string repositoryNameColumn)
    {
        var colIdx = ColumnLetterToIndex(repositoryNameColumn);
        var result = new List<(int RowIndex, string ProjectName)>();

        for (int i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (row.Count > colIdx)
            {
                var value = row[colIdx]?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    result.Add((i, value));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 解析 UniqueKeyColumn — 回傳 UK → row index 的映射（0-based）
    /// </summary>
    private static Dictionary<string, int> ParseUniqueKeyRows(
        IList<IList<object>> sheetData,
        string uniqueKeyColumn)
    {
        var colIdx = ColumnLetterToIndex(uniqueKeyColumn);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (row.Count > colIdx)
            {
                var value = row[colIdx]?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    result[value] = i;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 同步單一 Project 的資料至 Google Sheet
    /// </summary>
    private async Task SyncProjectAsync(
        string projectName,
        List<ConsolidatedReleaseEntry> entries,
        IList<IList<object>> sheetData,
        List<(int RowIndex, string ProjectName)> repoRows,
        Dictionary<string, int> existingUkMap,
        GoogleSheetOptions opts,
        int sheetId)
    {
        var colMap = opts.ColumnMapping;
        var modifiedRowIndices = new HashSet<int>();

        foreach (var entry in entries)
        {
            var uk = $"{entry.WorkItemId}{projectName}";

            if (existingUkMap.TryGetValue(uk, out var existingRowIndex))
            {
                // UK 已存在 — 更新
                await UpdateEntryAsync(entry, existingRowIndex, opts, sheetData);
                modifiedRowIndices.Add(existingRowIndex);
            }
            else
            {
                // UK 不存在 — 新增
                var insertRowIndex = CalculateInsertRowIndex(projectName, repoRows, sheetData.Count);

                await _googleSheetService.InsertRowAsync(opts.SpreadsheetId, sheetId, insertRowIndex);

                // 插入後，sheetData 行號需往下移（在 insertRowIndex 以後的所有行都 +1）
                ShiftRowIndices(repoRows, insertRowIndex);
                ShiftRowIndicesInMap(existingUkMap, insertRowIndex);
                ShiftRowIndicesInSet(modifiedRowIndices, insertRowIndex);

                // 將新空列插入 sheetData 以保持同步
                sheetData.Insert(insertRowIndex, new List<object>());

                // 填入欄位值
                await FillNewRowAsync(entry, uk, insertRowIndex, opts, sheetData);

                // 更新既有 UK 映射
                existingUkMap[uk] = insertRowIndex;
                modifiedRowIndices.Add(insertRowIndex);

                // 重新讀取 repoRows（因為 sheetData 已更新）
                repoRows = ParseRepositoryRows(sheetData, colMap.RepositoryNameColumn);
            }
        }

        // 7. 排序 Project 區塊
        if (modifiedRowIndices.Count > 0)
        {
            await SortProjectBlockAsync(projectName, repoRows, sheetData, opts);
        }
    }

    /// <summary>
    /// 更新既有列的 Authors 與 PullRequestUrls
    /// </summary>
    private async Task UpdateEntryAsync(
        ConsolidatedReleaseEntry entry,
        int rowIndex,
        GoogleSheetOptions opts,
        IList<IList<object>> sheetData)
    {
        var colMap = opts.ColumnMapping;
        var displayRowIndex = rowIndex + 1; // 1-based

        var authorsValue = string.Join("\n", entry.Authors
            .Select(a => a.AuthorName)
            .OrderBy(a => a));
        var prUrlsValue = string.Join("\n", entry.PullRequests
            .Select(p => p.Url)
            .OrderBy(u => u));

        var updates = new Dictionary<string, string>
        {
            [$"{colMap.AuthorsColumn}{displayRowIndex}"] = authorsValue,
            [$"{colMap.PullRequestUrlsColumn}{displayRowIndex}"] = prUrlsValue
        };

        await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);

        // 更新記憶體中的 sheetData
        var row = sheetData[rowIndex];
        SetCell(row, ColumnLetterToIndex(colMap.AuthorsColumn), authorsValue);
        SetCell(row, ColumnLetterToIndex(colMap.PullRequestUrlsColumn), prUrlsValue);

        _logger.LogInformation("更新列 {Row} 的 Authors 與 PullRequestUrls", displayRowIndex);
    }

    /// <summary>
    /// 填入新插入列的欄位值
    /// </summary>
    private async Task FillNewRowAsync(
        ConsolidatedReleaseEntry entry,
        string uk,
        int rowIndex,
        GoogleSheetOptions opts,
        IList<IList<object>> sheetData)
    {
        var colMap = opts.ColumnMapping;
        var displayRowIndex = rowIndex + 1; // 1-based

        var featureText = $"VSTS{entry.WorkItemId} - {entry.Title}";
        var featureValue = string.IsNullOrEmpty(entry.WorkItemUrl)
            ? featureText
            : $"=HYPERLINK(\"{entry.WorkItemUrl}\",\"{featureText}\")";

        var authorsValue = string.Join("\n", entry.Authors
            .Select(a => a.AuthorName)
            .OrderBy(a => a));
        var prUrlsValue = string.Join("\n", entry.PullRequests
            .Select(p => p.Url)
            .OrderBy(u => u));

        var updates = new Dictionary<string, string>
        {
            [$"{colMap.FeatureColumn}{displayRowIndex}"] = featureValue,
            [$"{colMap.TeamColumn}{displayRowIndex}"] = entry.TeamDisplayName,
            [$"{colMap.AuthorsColumn}{displayRowIndex}"] = authorsValue,
            [$"{colMap.PullRequestUrlsColumn}{displayRowIndex}"] = prUrlsValue,
            [$"{colMap.UniqueKeyColumn}{displayRowIndex}"] = uk,
            [$"{colMap.AutoSyncColumn}{displayRowIndex}"] = "TRUE"
        };

        await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);

        // 更新記憶體中的 sheetData
        var row = sheetData[rowIndex];
        SetCell(row, ColumnLetterToIndex(colMap.FeatureColumn), featureValue);
        SetCell(row, ColumnLetterToIndex(colMap.TeamColumn), entry.TeamDisplayName);
        SetCell(row, ColumnLetterToIndex(colMap.AuthorsColumn), authorsValue);
        SetCell(row, ColumnLetterToIndex(colMap.PullRequestUrlsColumn), prUrlsValue);
        SetCell(row, ColumnLetterToIndex(colMap.UniqueKeyColumn), uk);
        SetCell(row, ColumnLetterToIndex(colMap.AutoSyncColumn), "TRUE");

        _logger.LogInformation("填入新列 {Row} 的欄位值，UK={UK}", displayRowIndex, uk);
    }

    /// <summary>
    /// 計算新列應插入的 row index（0-based）
    /// </summary>
    private static int CalculateInsertRowIndex(
        string projectName,
        List<(int RowIndex, string ProjectName)> repoRows,
        int totalRows)
    {
        var projectIdx = repoRows.FindIndex(r => r.ProjectName == projectName);
        if (projectIdx < 0)
        {
            // 找不到 Project 標記列 — 插在最後
            return totalRows;
        }

        var currentRepoRow = repoRows[projectIdx];
        // 第一個 Project：在標記列的下一行插入
        return currentRepoRow.RowIndex + 1;
    }

    /// <summary>
    /// 排序指定 Project 區塊內的資料列
    /// </summary>
    private async Task SortProjectBlockAsync(
        string projectName,
        List<(int RowIndex, string ProjectName)> repoRows,
        IList<IList<object>> sheetData,
        GoogleSheetOptions opts)
    {
        var colMap = opts.ColumnMapping;
        var projectIdx = repoRows.FindIndex(r => r.ProjectName == projectName);
        if (projectIdx < 0) return;

        var startRow = repoRows[projectIdx].RowIndex + 1; // 資料列起始（標記列後一行）
        int endRow;

        if (projectIdx + 1 < repoRows.Count)
        {
            endRow = repoRows[projectIdx + 1].RowIndex - 1; // 下一個標記列前一行
        }
        else
        {
            endRow = sheetData.Count - 1;
        }

        if (startRow > endRow) return; // 無資料列

        // 讀取區塊資料列
        var blockRows = new List<(int OriginalRowIndex, IList<object> Row)>();
        for (int i = startRow; i <= endRow; i++)
        {
            blockRows.Add((i, sheetData[i]));
        }

        if (blockRows.Count <= 1) return;

        // 依多欄位排序（空白排最後）
        var sortedRows = blockRows
            .OrderBy(r => GetCellSortKey(r.Row, colMap.TeamColumn))
            .ThenBy(r => GetCellSortKey(r.Row, colMap.AuthorsColumn))
            .ThenBy(r => GetCellSortKey(r.Row, colMap.FeatureColumn))
            .ThenBy(r => GetCellSortKey(r.Row, colMap.UniqueKeyColumn))
            .ToList();

        // 檢查是否有順序變化
        var hasChanges = false;
        for (int i = 0; i < blockRows.Count; i++)
        {
            if (blockRows[i].OriginalRowIndex != sortedRows[i].OriginalRowIndex)
            {
                hasChanges = true;
                break;
            }
        }

        if (!hasChanges) return;

        _logger.LogInformation("排序 Project 區塊 '{ProjectName}'，共 {Count} 列", projectName, blockRows.Count);

        // 收集所有需要寫回的儲存格
        var updates = new Dictionary<string, string>();
        var columns = new[]
        {
            colMap.RepositoryNameColumn,
            colMap.FeatureColumn,
            colMap.TeamColumn,
            colMap.AuthorsColumn,
            colMap.PullRequestUrlsColumn,
            colMap.UniqueKeyColumn,
            colMap.AutoSyncColumn
        }.Distinct().ToArray();

        for (int i = 0; i < sortedRows.Count; i++)
        {
            var targetRowDisplay = startRow + i + 1; // 1-based
            var sourceRow = sortedRows[i].Row;

            foreach (var col in columns)
            {
                var colIdx = ColumnLetterToIndex(col);
                var value = colIdx < sourceRow.Count ? sourceRow[colIdx]?.ToString() ?? "" : "";
                updates[$"{col}{targetRowDisplay}"] = value;
            }

            // 更新記憶體中的 sheetData
            sheetData[startRow + i] = sortedRows[i].Row;
        }

        await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);
        _logger.LogInformation("排序寫回完成");
    }

    /// <summary>
    /// 取得排序鍵值（空白排最後）
    /// </summary>
    private static (int Priority, string Value) GetCellSortKey(IList<object> row, string columnLetter)
    {
        var colIdx = ColumnLetterToIndex(columnLetter);
        var value = colIdx < row.Count ? row[colIdx]?.ToString() ?? "" : "";
        return (string.IsNullOrEmpty(value) ? 1 : 0, value);
    }

    /// <summary>
    /// 將欄位字母轉為 0-based 索引（A=0, B=1, ...）
    /// </summary>
    public static int ColumnLetterToIndex(string letter)
    {
        if (string.IsNullOrEmpty(letter) || letter.Length != 1)
            throw new ArgumentException($"欄位字母必須為單一字母，實際值：{letter}", nameof(letter));
        return letter.ToUpperInvariant()[0] - 'A';
    }

    /// <summary>
    /// 設定記憶體列指定欄位的值（必要時擴展列長度）
    /// </summary>
    private static void SetCell(IList<object> row, int colIndex, string value)
    {
        while (row.Count <= colIndex)
        {
            row.Add("");
        }
        row[colIndex] = value;
    }

    /// <summary>
    /// 將 insertRowIndex 以後（包含）的 row index 都 +1
    /// </summary>
    private static void ShiftRowIndices(List<(int RowIndex, string ProjectName)> repoRows, int insertRowIndex)
    {
        for (int i = 0; i < repoRows.Count; i++)
        {
            if (repoRows[i].RowIndex >= insertRowIndex)
            {
                repoRows[i] = (repoRows[i].RowIndex + 1, repoRows[i].ProjectName);
            }
        }
    }

    /// <summary>
    /// 將 insertRowIndex 以後的 map 中的 row index 都 +1
    /// </summary>
    private static void ShiftRowIndicesInMap(Dictionary<string, int> map, int insertRowIndex)
    {
        var keys = map.Keys.ToList();
        foreach (var key in keys)
        {
            if (map[key] >= insertRowIndex)
            {
                map[key]++;
            }
        }
    }

    /// <summary>
    /// 將 insertRowIndex 以後的 set 中的 row index 都 +1
    /// </summary>
    private static void ShiftRowIndicesInSet(HashSet<int> set, int insertRowIndex)
    {
        var toUpdate = set.Where(idx => idx >= insertRowIndex).ToList();
        foreach (var idx in toUpdate)
        {
            set.Remove(idx);
            set.Add(idx + 1);
        }
    }
}
