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
/// 從 Redis 讀取整合後的 Release 資料，並同步到 Google Sheet：
/// - 新增：在對應 Project 區塊插入新列
/// - 更新：僅更新 Authors 與 PullRequestUrls 欄位
/// - 排序：每個 Project 區塊內依 Team、Authors、Feature、UniqueKey 排序
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
    /// <param name="redisService">Redis 服務</param>
    /// <param name="googleSheetService">Google Sheet 服務</param>
    /// <param name="options">Google Sheet 配置選項</param>
    /// <param name="logger">日誌記錄器</param>
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
        _logger.LogInformation("開始更新 Google Sheets 資訊");

        var config = _options.Value;
        var columnMapping = config.ColumnMapping;

        // 驗證 ColumnMapping
        ValidateColumnMapping(columnMapping);

        // 從 Redis 讀取整合資料
        var consolidatedResult = await LoadConsolidatedDataAsync();
        if (consolidatedResult == null) return;

        // 讀取 Google Sheet 資料
        var sheetData = await _googleSheetService.GetSheetDataAsync(config.SpreadsheetId, config.SheetName, "A:Z");
        if (sheetData == null)
        {
            _logger.LogWarning("無法讀取 Google Sheet 資料，任務結束");
            return;
        }

        // 解析既有資料
        var repoColumnIndex = ColumnLetterToIndex(columnMapping.RepositoryNameColumn);
        var ukColumnIndex = ColumnLetterToIndex(columnMapping.UniqueKeyColumn);

        var projectBlocks = ParseProjectBlocks(sheetData, repoColumnIndex);
        var existingUniqueKeys = ParseUniqueKeys(sheetData, ukColumnIndex);

        // 追蹤受影響的 Project，用於排序
        var affectedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 延遲取得 SheetId（僅在首次需要插入列時呼叫一次）
        int? sheetId = null;

        // 處理每個 Project 的資料
        foreach (var (projectName, entries) in consolidatedResult.Projects)
        {
            foreach (var entry in entries)
            {
                var uniqueKey = $"{entry.WorkItemId}{projectName}";

                if (existingUniqueKeys.TryGetValue(uniqueKey, out var existingRowIndex))
                {
                    // 更新既有資料
                    await UpdateExistingRowAsync(config, columnMapping, existingRowIndex , entry);
                    affectedProjects.Add(projectName);
                }
                else
                {
                    // 新增資料
                    var insertRowIndex = CalculateInsertRowIndex(projectBlocks, projectName);
                    if (insertRowIndex < 0)
                    {
                        _logger.LogWarning("找不到 Project '{ProjectName}' 的區塊，跳過新增", projectName);
                        continue;
                    }

                    sheetId ??= await _googleSheetService.GetSheetIdByNameAsync(config.SpreadsheetId, config.SheetName);
                    await _googleSheetService.InsertRowAsync(config.SpreadsheetId, sheetId.Value, insertRowIndex);
                    await FillNewRowAsync(config, columnMapping, insertRowIndex, entry, uniqueKey, projectName);
                    affectedProjects.Add(projectName);
                }
            }
        }

        // 重新讀取 Sheet 資料進行排序
        if (affectedProjects.Count > 0)
        {
            var updatedSheetData = await _googleSheetService.GetSheetDataAsync(config.SpreadsheetId, config.SheetName, "A:Z");
            if (updatedSheetData != null)
            {
                var updatedProjectBlocks = ParseProjectBlocks(updatedSheetData, repoColumnIndex);
                await SortAffectedProjectBlocksAsync(config, columnMapping, updatedSheetData, updatedProjectBlocks, affectedProjects);
            }
        }

        _logger.LogInformation("Google Sheets 更新完成");
    }

    /// <summary>
    /// 驗證所有欄位映射值是否在 A-Z 範圍內
    /// </summary>
    internal static void ValidateColumnMapping(ColumnMappingOptions mapping)
    {
        ValidateSingleColumn(mapping.RepositoryNameColumn, nameof(mapping.RepositoryNameColumn));
        ValidateSingleColumn(mapping.FeatureColumn, nameof(mapping.FeatureColumn));
        ValidateSingleColumn(mapping.TeamColumn, nameof(mapping.TeamColumn));
        ValidateSingleColumn(mapping.AuthorsColumn, nameof(mapping.AuthorsColumn));
        ValidateSingleColumn(mapping.PullRequestUrlsColumn, nameof(mapping.PullRequestUrlsColumn));
        ValidateSingleColumn(mapping.UniqueKeyColumn, nameof(mapping.UniqueKeyColumn));
        ValidateSingleColumn(mapping.AutoSyncColumn, nameof(mapping.AutoSyncColumn));
    }

    /// <summary>
    /// 驗證單一欄位設定值是否為 A-Z 單字母
    /// </summary>
    private static void ValidateSingleColumn(string value, string fieldName)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 1 || value[0] < 'A' || value[0] > 'Z')
        {
            throw new InvalidOperationException(
                $"ColumnMapping 設定無效：'{fieldName}' 的值 '{value}' 不在 A-Z 範圍內");
        }
    }

    /// <summary>
    /// 從 Redis 讀取整合資料
    /// </summary>
    private async Task<ConsolidatedReleaseResult?> LoadConsolidatedDataAsync()
    {
        var json = await _redisService.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("Redis Hash '{HashKey}:{Field}' 無整合資料，任務結束",
                RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
            return null;
        }

        var result = json.ToTypedObject<ConsolidatedReleaseResult>();
        if (result == null || result.Projects.Count == 0)
        {
            _logger.LogWarning("Redis 整合資料反序列化為空，任務結束");
            return null;
        }

        _logger.LogInformation("從 Redis 載入整合資料，共 {ProjectCount} 個專案", result.Projects.Count);
        return result;
    }

    /// <summary>
    /// 解析 RepositoryNameColumn 取得所有 Project 區塊（row index 與 ProjectName）
    /// </summary>
    internal static List<(int RowIndex, string ProjectName)> ParseProjectBlocks(
        IList<IList<object>> sheetData, int repoColumnIndex)
    {
        var blocks = new List<(int RowIndex, string ProjectName)>();

        for (var i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (repoColumnIndex < row.Count)
            {
                var cellValue = row[repoColumnIndex]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    blocks.Add((i, cellValue));
                }
            }
        }

        return blocks;
    }

    /// <summary>
    /// 解析 UniqueKeyColumn 取得所有既有 UK 值與 row index 的映射
    /// </summary>
    internal static Dictionary<string, int> ParseUniqueKeys(
        IList<IList<object>> sheetData, int ukColumnIndex)
    {
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (ukColumnIndex < row.Count)
            {
                var cellValue = row[ukColumnIndex]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    keys[cellValue] = i;
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// 計算新增列的插入位置（0-based row index）
    /// </summary>
    internal static int CalculateInsertRowIndex(
        List<(int RowIndex, string ProjectName)> projectBlocks,
        string projectName)
    {
        var blockIndex = projectBlocks.FindIndex(b =>
            string.Equals(b.ProjectName, projectName, StringComparison.OrdinalIgnoreCase));

        if (blockIndex < 0) return -1;

        var currentBlock = projectBlocks[blockIndex];

        return currentBlock.RowIndex + 1;
    }

    /// <summary>
    /// 更新既有列的 Authors 與 PullRequestUrls 欄位
    /// </summary>
    private async Task UpdateExistingRowAsync(
        GoogleSheetOptions config,
        ColumnMappingOptions columnMapping,
        int rowIndex,
        ConsolidatedReleaseEntry entry)
    {
        var sheetRowNumber = rowIndex + 1; // 轉為 1-based

        var updates = new Dictionary<string, object>
        {
            [$"{columnMapping.AuthorsColumn}{sheetRowNumber}"] = FormatAuthors(entry.Authors),
            [$"{columnMapping.PullRequestUrlsColumn}{sheetRowNumber}"] = FormatPullRequestUrls(entry.PullRequests)
        };

        await _googleSheetService.UpdateCellsAsync(config.SpreadsheetId, config.SheetName, updates);
        _logger.LogInformation("更新既有列 Row {RowNumber}：WorkItemId={WorkItemId}", sheetRowNumber, entry.WorkItemId);
    }

    /// <summary>
    /// 填入新增列的所有欄位值
    /// </summary>
    private async Task FillNewRowAsync(
        GoogleSheetOptions config,
        ColumnMappingOptions columnMapping,
        int rowIndex,
        ConsolidatedReleaseEntry entry,
        string uniqueKey,
        string projectName)
    {
        var sheetRowNumber = rowIndex + 1; // 轉為 1-based

        var featureValue = FormatFeatureWithHyperlink(entry);

        var updates = new Dictionary<string, object>
        {
            [$"{columnMapping.FeatureColumn}{sheetRowNumber}"] = featureValue,
            [$"{columnMapping.TeamColumn}{sheetRowNumber}"] = entry.TeamDisplayName,
            [$"{columnMapping.AuthorsColumn}{sheetRowNumber}"] = FormatAuthors(entry.Authors),
            [$"{columnMapping.PullRequestUrlsColumn}{sheetRowNumber}"] = FormatPullRequestUrls(entry.PullRequests),
            [$"{columnMapping.UniqueKeyColumn}{sheetRowNumber}"] = uniqueKey,
            [$"{columnMapping.AutoSyncColumn}{sheetRowNumber}"] = "TRUE"
        };

        await _googleSheetService.UpdateCellsAsync(config.SpreadsheetId, config.SheetName, updates);
        _logger.LogInformation("新增列 Row {RowNumber}：WorkItemId={WorkItemId}, Project={ProjectName}",
            sheetRowNumber, entry.WorkItemId, projectName);
    }

    /// <summary>
    /// 格式化 Feature 欄位，包含 HYPERLINK 公式
    /// </summary>
    internal static string FormatFeatureWithHyperlink(ConsolidatedReleaseEntry entry)
    {
        var displayText = $"VSTS{entry.WorkItemId} - {entry.Title}";

        if (string.IsNullOrEmpty(entry.WorkItemUrl))
        {
            return displayText;
        }

        return $"=HYPERLINK(\"{entry.WorkItemUrl}\", \"{displayText.Replace("\"", "\"\"")}\")";
    }

    /// <summary>
    /// 格式化 Authors 欄位（排序 + 換行分隔）
    /// </summary>
    internal static string FormatAuthors(List<ConsolidatedAuthorInfo> authors)
    {
        return string.Join("\n", authors
            .Select(a => a.AuthorName)
            .OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// 格式化 PullRequestUrls 欄位（排序 + 換行分隔）
    /// </summary>
    internal static string FormatPullRequestUrls(List<ConsolidatedPrInfo> pullRequests)
    {
        return string.Join("\n", pullRequests
            .Select(p => p.Url)
            .OrderBy(url => url, StringComparer.Ordinal));
    }

    /// <summary>
    /// 對受影響的 Project 區塊進行排序
    /// </summary>
    private async Task SortAffectedProjectBlocksAsync(
        GoogleSheetOptions config,
        ColumnMappingOptions columnMapping,
        IList<IList<object>> sheetData,
        List<(int RowIndex, string ProjectName)> projectBlocks,
        HashSet<string> affectedProjects)
    {
        var teamColumnIndex = ColumnLetterToIndex(columnMapping.TeamColumn);
        var authorsColumnIndex = ColumnLetterToIndex(columnMapping.AuthorsColumn);
        var featureColumnIndex = ColumnLetterToIndex(columnMapping.FeatureColumn);
        var ukColumnIndex = ColumnLetterToIndex(columnMapping.UniqueKeyColumn);

        for (var i = 0; i < projectBlocks.Count; i++)
        {
            var (blockRowIndex, projectName) = projectBlocks[i];

            if (!affectedProjects.Contains(projectName)) continue;

            // 計算資料列範圍（排除 RepositoryNameColumn 標記列）
            var dataStartRow = blockRowIndex + 1;
            var dataEndRow = i < projectBlocks.Count - 1
                ? projectBlocks[i + 1].RowIndex
                : sheetData.Count;

            if (dataStartRow >= dataEndRow) continue;

            // 讀取資料列
            var dataRows = new List<IList<object>>();
            for (var r = dataStartRow; r < dataEndRow; r++)
            {
                dataRows.Add(sheetData[r]);
            }

            // 排序：Team → Authors → Feature → UniqueKey，空白排最後
            var sortedRows = dataRows
                .OrderBy(row => GetCellValueForSort(row, teamColumnIndex), new EmptyLastComparer())
                .ThenBy(row => GetCellValueForSort(row, authorsColumnIndex), new EmptyLastComparer())
                .ThenBy(row => GetCellValueForSort(row, featureColumnIndex), new EmptyLastComparer())
                .ThenBy(row => GetCellValueForSort(row, ukColumnIndex), new EmptyLastComparer())
                .ToList();

            // 寫回排序後的資料
            await WriteSortedDataAsync(config, columnMapping, sortedRows, dataStartRow);
        }
    }

    /// <summary>
    /// 將排序後的資料寫回 Google Sheet
    /// </summary>
    private async Task WriteSortedDataAsync(
        GoogleSheetOptions config,
        ColumnMappingOptions columnMapping,
        List<IList<object>> sortedRows,
        int startRowIndex)
    {
        var updates = new Dictionary<string, object>();

        // 需要寫回的欄位
        var columns = new[]
        {
            (columnMapping.FeatureColumn, ColumnLetterToIndex(columnMapping.FeatureColumn)),
            (columnMapping.TeamColumn, ColumnLetterToIndex(columnMapping.TeamColumn)),
            (columnMapping.AuthorsColumn, ColumnLetterToIndex(columnMapping.AuthorsColumn)),
            (columnMapping.PullRequestUrlsColumn, ColumnLetterToIndex(columnMapping.PullRequestUrlsColumn)),
            (columnMapping.UniqueKeyColumn, ColumnLetterToIndex(columnMapping.UniqueKeyColumn)),
            (columnMapping.AutoSyncColumn, ColumnLetterToIndex(columnMapping.AutoSyncColumn))
        };

        for (var r = 0; r < sortedRows.Count; r++)
        {
            var row = sortedRows[r];
            var sheetRowNumber = startRowIndex + r + 1; // 轉為 1-based

            foreach (var (columnLetter, columnIndex) in columns)
            {
                var value = columnIndex < row.Count ? row[columnIndex]?.ToString() ?? string.Empty : string.Empty;
                updates[$"{columnLetter}{sheetRowNumber}"] = value;
            }
        }

        if (updates.Count > 0)
        {
            await _googleSheetService.UpdateCellsAsync(config.SpreadsheetId, config.SheetName, updates);
        }
    }

    /// <summary>
    /// 取得儲存格值用於排序
    /// </summary>
    private static string GetCellValueForSort(IList<object> row, int columnIndex)
    {
        if (columnIndex >= row.Count) return string.Empty;
        return row[columnIndex]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 將欄位字母轉換為 0-based 索引（如 A=0, B=1, Z=25）
    /// </summary>
    internal static int ColumnLetterToIndex(string columnLetter)
    {
        return columnLetter[0] - 'A';
    }

    /// <summary>
    /// 空白值排最後的比較器
    /// </summary>
    private sealed class EmptyLastComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var xEmpty = string.IsNullOrEmpty(x);
            var yEmpty = string.IsNullOrEmpty(y);

            if (xEmpty && yEmpty) return 0;
            if (xEmpty) return 1;
            if (yEmpty) return -1;

            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}

