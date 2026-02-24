using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 更新 Google Sheets 資訊任務，從 Redis 讀取整合資料並同步至 Google Sheet
/// </summary>
public class UpdateGoogleSheetsTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IGoogleSheetService _googleSheetService;
    private readonly GoogleSheetOptions _options;
    private readonly ILogger<UpdateGoogleSheetsTask> _logger;

    /// <summary>
    /// 初始化 <see cref="UpdateGoogleSheetsTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="googleSheetService">Google Sheet 服務</param>
    /// <param name="options">Google Sheet 設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public UpdateGoogleSheetsTask(
        IRedisService redisService,
        IGoogleSheetService googleSheetService,
        IOptions<GoogleSheetOptions> options,
        ILogger<UpdateGoogleSheetsTask> logger)
    {
        _redisService = redisService;
        _googleSheetService = googleSheetService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 執行更新 Google Sheets 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始同步整合資料至 Google Sheet");

        ValidateColumnMapping(_options.ColumnMapping);

        var consolidatedResult = await ReadConsolidatedDataAsync();
        if (consolidatedResult == null) return;

        var sheetId = await ResolveSheetIdAsync();
        if (sheetId == null) return;

        var sheetData = await ReadSheetDataAsync();
        if (sheetData == null) return;

        var repoColIndex = ColumnLetterToIndex(_options.ColumnMapping.RepositoryNameColumn);
        var uniqueKeyColIndex = ColumnLetterToIndex(_options.ColumnMapping.UniqueKeyColumn);

        var segments = BuildProjectSegments(sheetData, repoColIndex);
        var existingUniqueKeys = BuildUniqueKeyMap(sheetData, uniqueKeyColIndex);

        var (insertItems, updateItems) = ClassifyItems(consolidatedResult.Projects, existingUniqueKeys);
        _logger.LogInformation("分類完成：新增 {InsertCount} 筆，更新 {UpdateCount} 筆",
            insertItems.Count, updateItems.Count);

        var insertsByProject = insertItems
            .GroupBy(i => i.ProjectName)
            .ToDictionary(g => g.Key, g => g.ToList());

        sheetData = await InsertNewRowsAsync(insertsByProject, segments, sheetId.Value);
        if (sheetData == null) return;

        segments = BuildProjectSegments(sheetData, repoColIndex);
        existingUniqueKeys = BuildUniqueKeyMap(sheetData, uniqueKeyColIndex);

        var (batchUpdates, affectedProjects) =
            await BuildInsertBatchUpdatesAsync(insertsByProject, segments, sheetId.Value);

        AppendUpdateBatchUpdates(batchUpdates, updateItems, existingUniqueKeys, affectedProjects);

        if (batchUpdates.Count > 0)
        {
            await _googleSheetService.BatchUpdateCellsAsync(_options.SpreadsheetId, batchUpdates);
            _logger.LogInformation("批次更新 {Count} 個儲存格範圍", batchUpdates.Count);
        }

        await SortAffectedProjectsAsync(affectedProjects, repoColIndex);

        _logger.LogInformation("Google Sheet 同步完成");
    }

    /// <summary>
    /// 從 Redis 讀取整合資料，若無資料則回傳 null
    /// </summary>
    private async Task<ConsolidatedReleaseResult?> ReadConsolidatedDataAsync()
    {
        var consolidatedJson = await _redisService.HashGetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);

        if (string.IsNullOrEmpty(consolidatedJson))
        {
            _logger.LogInformation("Redis 中沒有整合資料，結束同步");
            return null;
        }

        var result = consolidatedJson.ToTypedObject<ConsolidatedReleaseResult>();
        if (result?.Projects == null || result.Projects.Count == 0)
        {
            _logger.LogInformation("Redis 中沒有整合資料，結束同步");
            return null;
        }

        return result;
    }

    /// <summary>
    /// 動態取得工作表 SheetId，若找不到則回傳 null
    /// </summary>
    private async Task<int?> ResolveSheetIdAsync()
    {
        var sheetId = await _googleSheetService.GetSheetIdByNameAsync(
            _options.SpreadsheetId, _options.SheetName);

        if (sheetId == null)
            _logger.LogWarning("找不到工作表 '{SheetName}'，結束同步", _options.SheetName);

        return sheetId;
    }

    /// <summary>
    /// 讀取工作表 A:Z 範圍資料，若失敗則回傳 null
    /// </summary>
    private async Task<IList<IList<object>>?> ReadSheetDataAsync()
    {
        var data = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (data == null)
            _logger.LogWarning("無法讀取工作表 '{SheetName}' 的資料，結束同步", _options.SheetName);

        return data;
    }

    /// <summary>
    /// 將整合資料分類為新增與更新清單
    /// </summary>
    private static (
        List<(string ProjectName, ConsolidatedReleaseEntry Entry)> InsertItems,
        List<(string ProjectName, ConsolidatedReleaseEntry Entry, int RowIndex)> UpdateItems)
        ClassifyItems(
            Dictionary<string, List<ConsolidatedReleaseEntry>> projects,
            Dictionary<string, int> existingUniqueKeys)
    {
        var insertItems = new List<(string ProjectName, ConsolidatedReleaseEntry Entry)>();
        var updateItems = new List<(string ProjectName, ConsolidatedReleaseEntry Entry, int RowIndex)>();

        foreach (var (projectName, entries) in projects)
        {
            foreach (var entry in entries)
            {
                var uniqueKey = $"{entry.WorkItemId}{projectName}";
                if (existingUniqueKeys.TryGetValue(uniqueKey, out var rowIndex))
                    updateItems.Add((projectName, entry, rowIndex));
                else
                    insertItems.Add((projectName, entry));
            }
        }

        return (insertItems, updateItems);
    }

    /// <summary>
    /// 在各專案區段插入空白列，並重新讀取工作表資料
    /// </summary>
    private async Task<IList<IList<object>>?> InsertNewRowsAsync(
        Dictionary<string, List<(string ProjectName, ConsolidatedReleaseEntry Entry)>> insertsByProject,
        List<SheetProjectSegment> segments,
        int sheetId)
    {
        var insertPositions = new List<(int RowIndex, int Count, string ProjectName)>();

        foreach (var (projectName, items) in insertsByProject)
        {
            var segment = segments.FirstOrDefault(s => s.ProjectName == projectName);
            if (segment == null)
            {
                _logger.LogWarning("在 Sheet 中找不到專案 '{ProjectName}' 的區段，跳過新增", projectName);
                continue;
            }

            insertPositions.Add((segment.DataStartRowIndex, items.Count, projectName));
        }

        foreach (var (rowIndex, count, projectName) in insertPositions.OrderByDescending(p => p.RowIndex))
        {
            await _googleSheetService.InsertRowsAsync(_options.SpreadsheetId, sheetId, rowIndex, count);
            _logger.LogInformation("在專案 '{ProjectName}' 的第 {Row} 列插入 {Count} 列空白列",
                projectName, rowIndex, count);
        }

        var refreshed = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (refreshed == null)
            _logger.LogWarning("插入空白列後無法重新讀取工作表資料，結束同步");

        return refreshed;
    }

    /// <summary>
    /// 為新增列建立批次更新資料
    /// </summary>
    private async Task<(
        List<(string Range, IList<IList<object>> Values)> BatchUpdates,
        HashSet<string> AffectedProjects)>
        BuildInsertBatchUpdatesAsync(
            Dictionary<string, List<(string ProjectName, ConsolidatedReleaseEntry Entry)>> insertsByProject,
            List<SheetProjectSegment> segments,
            int sheetId)
    {
        var batchUpdates = new List<(string Range, IList<IList<object>> Values)>();
        var affectedProjects = new HashSet<string>();
        var columnMapping = _options.ColumnMapping;

        foreach (var (projectName, items) in insertsByProject)
        {
            var segment = segments.FirstOrDefault(s => s.ProjectName == projectName);
            if (segment == null) continue;

            affectedProjects.Add(projectName);

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i].Entry;
                var rowIndex = segment.DataStartRowIndex + i;
                var row1Based = rowIndex + 1;
                var uniqueKey = $"{entry.WorkItemId}{projectName}";

                await AddFeatureCellAsync(batchUpdates, entry, sheetId, rowIndex, row1Based, columnMapping);
                AddInsertRowCells(batchUpdates, entry, projectName, row1Based, uniqueKey, columnMapping);
            }
        }

        return (batchUpdates, affectedProjects);
    }

    /// <summary>
    /// 新增 Feature 欄位資料（含超連結或純文字）
    /// </summary>
    private async Task AddFeatureCellAsync(
        List<(string Range, IList<IList<object>> Values)> batchUpdates,
        ConsolidatedReleaseEntry entry,
        int sheetId,
        int rowIndex,
        int row1Based,
        ColumnMappingOptions columnMapping)
    {
        var featureDisplayText = $"VSTS{entry.WorkItemId} - {entry.Title}";

        if (!string.IsNullOrEmpty(entry.WorkItemUrl))
        {
            await _googleSheetService.UpdateCellWithHyperlinkAsync(
                _options.SpreadsheetId, sheetId, rowIndex,
                ColumnLetterToIndex(columnMapping.FeatureColumn),
                featureDisplayText, entry.WorkItemUrl);
        }
        else
        {
            batchUpdates.Add((
                $"'{_options.SheetName}'!{columnMapping.FeatureColumn}{row1Based}",
                new List<IList<object>> { new List<object> { featureDisplayText } }));
        }
    }

    /// <summary>
    /// 新增插入列的其他欄位資料（Team、Authors、PRUrls、UniqueKey、AutoSync）
    /// </summary>
    private void AddInsertRowCells(
        List<(string Range, IList<IList<object>> Values)> batchUpdates,
        ConsolidatedReleaseEntry entry,
        string projectName,
        int row1Based,
        string uniqueKey,
        ColumnMappingOptions columnMapping)
    {
        var authorsValue = string.Join("\n",
            entry.Authors.OrderBy(a => a.AuthorName).Select(a => a.AuthorName));
        var prUrlsValue = string.Join("\n",
            entry.PullRequests.OrderBy(p => p.Url).Select(p => p.Url));

        batchUpdates.Add((
            $"'{_options.SheetName}'!{columnMapping.TeamColumn}{row1Based}",
            new List<IList<object>> { new List<object> { entry.TeamDisplayName } }));
        batchUpdates.Add((
            $"'{_options.SheetName}'!{columnMapping.AuthorsColumn}{row1Based}",
            new List<IList<object>> { new List<object> { authorsValue } }));
        batchUpdates.Add((
            $"'{_options.SheetName}'!{columnMapping.PullRequestUrlsColumn}{row1Based}",
            new List<IList<object>> { new List<object> { prUrlsValue } }));
        batchUpdates.Add((
            $"'{_options.SheetName}'!{columnMapping.UniqueKeyColumn}{row1Based}",
            new List<IList<object>> { new List<object> { uniqueKey } }));
        batchUpdates.Add((
            $"'{_options.SheetName}'!{columnMapping.AutoSyncColumn}{row1Based}",
            new List<IList<object>> { new List<object> { "TRUE" } }));
    }

    /// <summary>
    /// 將更新項目的 Authors 與 PullRequestUrls 加入批次更新清單
    /// </summary>
    private void AppendUpdateBatchUpdates(
        List<(string Range, IList<IList<object>> Values)> batchUpdates,
        List<(string ProjectName, ConsolidatedReleaseEntry Entry, int RowIndex)> updateItems,
        Dictionary<string, int> existingUniqueKeys,
        HashSet<string> affectedProjects)
    {
        var columnMapping = _options.ColumnMapping;

        foreach (var (projectName, entry, _) in updateItems)
        {
            var uniqueKey = $"{entry.WorkItemId}{projectName}";
            if (!existingUniqueKeys.TryGetValue(uniqueKey, out var currentRowIndex)) continue;

            affectedProjects.Add(projectName);
            var row1Based = currentRowIndex + 1;

            var authorsValue = string.Join("\n",
                entry.Authors.OrderBy(a => a.AuthorName).Select(a => a.AuthorName));
            var prUrlsValue = string.Join("\n",
                entry.PullRequests.OrderBy(p => p.Url).Select(p => p.Url));

            batchUpdates.Add((
                $"'{_options.SheetName}'!{columnMapping.AuthorsColumn}{row1Based}",
                new List<IList<object>> { new List<object> { authorsValue } }));
            batchUpdates.Add((
                $"'{_options.SheetName}'!{columnMapping.PullRequestUrlsColumn}{row1Based}",
                new List<IList<object>> { new List<object> { prUrlsValue } }));
        }
    }

    /// <summary>
    /// 重新讀取工作表，對受影響的專案區段在記憶體中排序後寫回
    /// </summary>
    private async Task SortAffectedProjectsAsync(HashSet<string> affectedProjects, int repoColIndex)
    {
        if (affectedProjects.Count == 0) return;

        var sortSheetData = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (sortSheetData == null) return;

        var columnMapping = _options.ColumnMapping;
        var sortSegments = BuildProjectSegments(sortSheetData, repoColIndex);
        var teamColIdx = ColumnLetterToIndex(columnMapping.TeamColumn);
        var authorsColIdx = ColumnLetterToIndex(columnMapping.AuthorsColumn);
        var featureColIdx = ColumnLetterToIndex(columnMapping.FeatureColumn);
        var uniqueKeyColIdx = ColumnLetterToIndex(columnMapping.UniqueKeyColumn);

        var sortBatchUpdates = new List<(string Range, IList<IList<object>> Values)>();

        foreach (var projectName in affectedProjects)
        {
            var segment = sortSegments.FirstOrDefault(s => s.ProjectName == projectName);
            if (segment == null || segment.DataStartRowIndex > segment.DataEndRowIndex) continue;

            var sortedRows = BuildSortedRows(sortSheetData, segment, teamColIdx, authorsColIdx, featureColIdx, uniqueKeyColIdx);
            var startRow1Based = segment.DataStartRowIndex + 1;
            var endRow1Based = segment.DataEndRowIndex + 1;

            sortBatchUpdates.Add((
                $"'{_options.SheetName}'!A{startRow1Based}:Z{endRow1Based}",
                sortedRows));

            _logger.LogInformation("排序專案 '{ProjectName}' 區段（列 {Start}-{End}）",
                projectName, segment.DataStartRowIndex, segment.DataEndRowIndex);
        }

        if (sortBatchUpdates.Count > 0)
        {
            await _googleSheetService.BatchUpdateCellsAsync(_options.SpreadsheetId, sortBatchUpdates);
            _logger.LogInformation("排序完成，更新 {Count} 個專案區段", sortBatchUpdates.Count);
        }
    }

    /// <summary>
    /// 對指定專案區段的資料列在記憶體中排序，feature 有值的排前面，空白的排後面
    /// </summary>
    private static List<IList<object>> BuildSortedRows(
        IList<IList<object>> sheetData,
        SheetProjectSegment segment,
        int teamColIdx, int authorsColIdx, int featureColIdx, int uniqueKeyColIdx)
    {
        var dataRows = sheetData
            .Skip(segment.DataStartRowIndex)
            .Take(segment.DataEndRowIndex - segment.DataStartRowIndex + 1)
            .ToList();

        var rowsByFeatureFilled = dataRows.ToLookup(r =>
            !string.IsNullOrEmpty(GetCellStringValue(r, featureColIdx)));

        return rowsByFeatureFilled[true]
            .OrderBy(r => SortKeyEmptyLast(r, teamColIdx))
            .ThenBy(r => SortKeyEmptyLast(r, authorsColIdx))
            .ThenBy(r => SortKeyEmptyLast(r, featureColIdx))
            .ThenBy(r => SortKeyEmptyLast(r, uniqueKeyColIdx))
            .Concat(rowsByFeatureFilled[false])
            .Select(PadRowTo26)
            .ToList<IList<object>>();
    }

    /// <summary>
    /// 驗證欄位對應設定是否在 A–Z 範圍內
    /// </summary>
    internal static void ValidateColumnMapping(ColumnMappingOptions columnMapping)
    {
        ValidateSingleColumn(columnMapping.RepositoryNameColumn, nameof(columnMapping.RepositoryNameColumn));
        ValidateSingleColumn(columnMapping.FeatureColumn, nameof(columnMapping.FeatureColumn));
        ValidateSingleColumn(columnMapping.TeamColumn, nameof(columnMapping.TeamColumn));
        ValidateSingleColumn(columnMapping.AuthorsColumn, nameof(columnMapping.AuthorsColumn));
        ValidateSingleColumn(columnMapping.PullRequestUrlsColumn, nameof(columnMapping.PullRequestUrlsColumn));
        ValidateSingleColumn(columnMapping.UniqueKeyColumn, nameof(columnMapping.UniqueKeyColumn));
        ValidateSingleColumn(columnMapping.AutoSyncColumn, nameof(columnMapping.AutoSyncColumn));
    }

    /// <summary>
    /// 驗證單一欄位值是否為 A–Z 的單一字母
    /// </summary>
    private static void ValidateSingleColumn(string columnValue, string columnName)
    {
        if (string.IsNullOrEmpty(columnValue) ||
            columnValue.Length != 1 ||
            columnValue[0] < 'A' || columnValue[0] > 'Z')
        {
            throw new InvalidOperationException(
                $"欄位 {columnName} 的值 '{columnValue}' 超出 A–Z 範圍");
        }
    }

    /// <summary>
    /// 從 Sheet 資料建立專案區段索引
    /// </summary>
    internal static List<SheetProjectSegment> BuildProjectSegments(
        IList<IList<object>> sheetData, int repoColIndex)
    {
        var segments = new List<SheetProjectSegment>();

        for (var i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (repoColIndex < row.Count)
            {
                var cellValue = row[repoColIndex]?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    segments.Add(new SheetProjectSegment
                    {
                        ProjectName = cellValue,
                        HeaderRowIndex = i
                    });
                }
            }
        }

        // 計算每個區段的資料範圍
        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].DataStartRowIndex = segments[i].HeaderRowIndex + 1;
            segments[i].DataEndRowIndex = i < segments.Count - 1
                ? segments[i + 1].HeaderRowIndex - 1
                : sheetData.Count - 1;
        }

        return segments;
    }

    /// <summary>
    /// 建立 UniqueKey 到 row index 的映射
    /// </summary>
    private static Dictionary<string, int> BuildUniqueKeyMap(
        IList<IList<object>> sheetData, int uniqueKeyColIndex)
    {
        var map = new Dictionary<string, int>();

        for (var i = 0; i < sheetData.Count; i++)
        {
            var row = sheetData[i];
            if (uniqueKeyColIndex < row.Count)
            {
                var cellValue = row[uniqueKeyColIndex]?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    map[cellValue] = i;
                }
            }
        }

        return map;
    }

    /// <summary>
    /// 將欄位字母轉換為 0-based 索引（A=0, B=1, ..., Z=25）
    /// </summary>
    internal static int ColumnLetterToIndex(string column)
    {
        return column[0] - 'A';
    }

    /// <summary>
    /// 取得列中指定欄位的字串值
    /// </summary>
    private static string GetCellStringValue(IList<object> row, int colIndex)
        => colIndex < row.Count ? row[colIndex]?.ToString() ?? string.Empty : string.Empty;

    /// <summary>
    /// 排序鍵：空白欄位排在最後面
    /// </summary>
    private static (int, string) SortKeyEmptyLast(IList<object> row, int colIndex)
    {
        var value = GetCellStringValue(row, colIndex);
        return (string.IsNullOrEmpty(value) ? 1 : 0, value);
    }

    /// <summary>
    /// 將列補齊至 26 欄（A–Z），不足部分填入空字串
    /// </summary>
    private static IList<object> PadRowTo26(IList<object> row)
    {
        var padded = new List<object>(row);
        while (padded.Count < 26)
            padded.Add(string.Empty);
        return padded;
    }
}

/// <summary>
/// 代表 Google Sheet 中一個專案區段的位置資訊
/// </summary>
internal class SheetProjectSegment
{
    /// <summary>
    /// 專案名稱
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 專案表頭列的 0-based row index
    /// </summary>
    public int HeaderRowIndex { get; set; }

    /// <summary>
    /// 資料起始列的 0-based row index
    /// </summary>
    public int DataStartRowIndex { get; set; }

    /// <summary>
    /// 資料結束列的 0-based row index
    /// </summary>
    public int DataEndRowIndex { get; set; }
}
