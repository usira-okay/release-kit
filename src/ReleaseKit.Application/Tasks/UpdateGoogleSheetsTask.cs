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

        // US3: 驗證欄位對應設定
        ValidateColumnMapping(_options.ColumnMapping);

        // US2: 讀取 Redis 整合資料
        var consolidatedJson = await _redisService.HashGetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);

        if (string.IsNullOrEmpty(consolidatedJson))
        {
            _logger.LogInformation("Redis 中沒有整合資料，結束同步");
            return;
        }

        var consolidatedResult = consolidatedJson.ToTypedObject<ConsolidatedReleaseResult>();
        if (consolidatedResult?.Projects == null || consolidatedResult.Projects.Count == 0)
        {
            _logger.LogInformation("Redis 中沒有整合資料，結束同步");
            return;
        }

        // US3: 動態取得 SheetId
        var sheetId = await _googleSheetService.GetSheetIdByNameAsync(
            _options.SpreadsheetId, _options.SheetName);

        if (sheetId == null)
        {
            _logger.LogWarning("找不到工作表 '{SheetName}'，結束同步", _options.SheetName);
            return;
        }

        // 讀取 Sheet 資料
        var sheetData = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (sheetData == null)
        {
            _logger.LogWarning("無法讀取工作表 '{SheetName}' 的資料，結束同步", _options.SheetName);
            return;
        }

        var columnMapping = _options.ColumnMapping;
        var repoColIndex = ColumnLetterToIndex(columnMapping.RepositoryNameColumn);
        var uniqueKeyColIndex = ColumnLetterToIndex(columnMapping.UniqueKeyColumn);

        // 建立專案區段索引
        var segments = BuildProjectSegments(sheetData, repoColIndex);

        // 建立既有 UniqueKey → row index 映射
        var existingUniqueKeys = BuildUniqueKeyMap(sheetData, uniqueKeyColIndex);

        // 分類 Insert/Update
        var insertItems = new List<(string ProjectName, ConsolidatedReleaseEntry Entry)>();
        var updateItems = new List<(string ProjectName, ConsolidatedReleaseEntry Entry, int RowIndex)>();

        foreach (var (projectName, entries) in consolidatedResult.Projects)
        {
            foreach (var entry in entries)
            {
                var uniqueKey = $"{entry.WorkItemId}{projectName}";
                if (existingUniqueKeys.TryGetValue(uniqueKey, out var rowIndex))
                {
                    updateItems.Add((projectName, entry, rowIndex));
                }
                else
                {
                    insertItems.Add((projectName, entry));
                }
            }
        }

        _logger.LogInformation("分類完成：新增 {InsertCount} 筆，更新 {UpdateCount} 筆",
            insertItems.Count, updateItems.Count);

        // 批次插入空白列（從最後一列往前，避免 index 偏移）
        var insertsByProject = insertItems
            .GroupBy(i => i.ProjectName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 計算每個專案的插入位置，從最後往前插入
        var insertPositions = new List<(int RowIndex, int Count, string ProjectName)>();
        foreach (var (projectName, items) in insertsByProject)
        {
            var segment = segments.FirstOrDefault(s => s.ProjectName == projectName);
            if (segment == null)
            {
                _logger.LogWarning("在 Sheet 中找不到專案 '{ProjectName}' 的區段，跳過新增", projectName);
                continue;
            }

            var insertRowIndex = segment.DataStartRowIndex;
            insertPositions.Add((insertRowIndex, items.Count, projectName));
        }

        // 從最後一列往前插入以避免 index 偏移
        foreach (var (rowIndex, count, projectName) in insertPositions.OrderByDescending(p => p.RowIndex))
        {
            await _googleSheetService.InsertRowsAsync(
                _options.SpreadsheetId, sheetId.Value, rowIndex, count);
            _logger.LogInformation("在專案 '{ProjectName}' 的第 {Row} 列插入 {Count} 列空白列",
                projectName, rowIndex, count);
        }

        // 插入後重新讀取 Sheet 以取得正確的 row index
        sheetData = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (sheetData == null)
        {
            _logger.LogWarning("插入空白列後無法重新讀取工作表資料，結束同步");
            return;
        }

        // 重新建立索引
        segments = BuildProjectSegments(sheetData, repoColIndex);
        existingUniqueKeys = BuildUniqueKeyMap(sheetData, uniqueKeyColIndex);

        // 填入新增資料
        var batchUpdates = new List<(string Range, IList<IList<object>> Values)>();
        var affectedProjects = new HashSet<string>();

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

                // FeatureColumn - 使用 HYPERLINK
                var featureDisplayText = $"VSTS{entry.WorkItemId} - {entry.Title}";
                if (!string.IsNullOrEmpty(entry.WorkItemUrl))
                {
                    await _googleSheetService.UpdateCellWithHyperlinkAsync(
                        _options.SpreadsheetId, sheetId.Value, rowIndex,
                        ColumnLetterToIndex(columnMapping.FeatureColumn),
                        featureDisplayText, entry.WorkItemUrl);
                }
                else
                {
                    batchUpdates.Add((
                        $"'{_options.SheetName}'!{columnMapping.FeatureColumn}{row1Based}",
                        new List<IList<object>> { new List<object> { featureDisplayText } }));
                }

                // TeamColumn
                batchUpdates.Add((
                    $"'{_options.SheetName}'!{columnMapping.TeamColumn}{row1Based}",
                    new List<IList<object>> { new List<object> { entry.TeamDisplayName } }));

                // AuthorsColumn - 依 authorName 排序後換行分隔
                var authorsValue = string.Join("\n",
                    entry.Authors.OrderBy(a => a.AuthorName).Select(a => a.AuthorName));
                batchUpdates.Add((
                    $"'{_options.SheetName}'!{columnMapping.AuthorsColumn}{row1Based}",
                    new List<IList<object>> { new List<object> { authorsValue } }));

                // PullRequestUrlsColumn - 依 url 排序後換行分隔
                var prUrlsValue = string.Join("\n",
                    entry.PullRequests.OrderBy(p => p.Url).Select(p => p.Url));
                batchUpdates.Add((
                    $"'{_options.SheetName}'!{columnMapping.PullRequestUrlsColumn}{row1Based}",
                    new List<IList<object>> { new List<object> { prUrlsValue } }));

                // UniqueKeyColumn
                batchUpdates.Add((
                    $"'{_options.SheetName}'!{columnMapping.UniqueKeyColumn}{row1Based}",
                    new List<IList<object>> { new List<object> { uniqueKey } }));

                // AutoSyncColumn
                batchUpdates.Add((
                    $"'{_options.SheetName}'!{columnMapping.AutoSyncColumn}{row1Based}",
                    new List<IList<object>> { new List<object> { "TRUE" } }));
            }
        }

        // 更新既有列（僅更新 AuthorsColumn 與 PullRequestUrlsColumn）
        // 需要重新計算 row index（因為插入了新列）
        var updatedUniqueKeys = BuildUniqueKeyMap(sheetData, uniqueKeyColIndex);

        foreach (var (projectName, entry, _) in updateItems)
        {
            var uniqueKey = $"{entry.WorkItemId}{projectName}";
            if (!updatedUniqueKeys.TryGetValue(uniqueKey, out var currentRowIndex)) continue;

            affectedProjects.Add(projectName);
            var row1Based = currentRowIndex + 1;

            // AuthorsColumn
            var authorsValue = string.Join("\n",
                entry.Authors.OrderBy(a => a.AuthorName).Select(a => a.AuthorName));
            batchUpdates.Add((
                $"'{_options.SheetName}'!{columnMapping.AuthorsColumn}{row1Based}",
                new List<IList<object>> { new List<object> { authorsValue } }));

            // PullRequestUrlsColumn
            var prUrlsValue = string.Join("\n",
                entry.PullRequests.OrderBy(p => p.Url).Select(p => p.Url));
            batchUpdates.Add((
                $"'{_options.SheetName}'!{columnMapping.PullRequestUrlsColumn}{row1Based}",
                new List<IList<object>> { new List<object> { prUrlsValue } }));
        }

        // 執行批次更新
        if (batchUpdates.Count > 0)
        {
            await _googleSheetService.BatchUpdateCellsAsync(_options.SpreadsheetId, batchUpdates);
            _logger.LogInformation("批次更新 {Count} 個儲存格範圍", batchUpdates.Count);
        }

        // 排序受影響的專案區段（在記憶體中排序後再寫回）
        // 重新讀取 Sheet 以取得批次更新後的最新資料
        var sortSheetData = await _googleSheetService.GetSheetDataAsync(
            _options.SpreadsheetId, $"'{_options.SheetName}'!A:Z");

        if (sortSheetData != null)
        {
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

                var dataRows = sortSheetData
                    .Skip(segment.DataStartRowIndex)
                    .Take(segment.DataEndRowIndex - segment.DataStartRowIndex + 1)
                    .ToList();

                var sortedRows = dataRows
                    .OrderBy(r => GetCellStringValue(r, teamColIdx))
                    .ThenBy(r => GetCellStringValue(r, authorsColIdx))
                    .ThenBy(r => GetCellStringValue(r, featureColIdx))
                    .ThenBy(r => GetCellStringValue(r, uniqueKeyColIdx))
                    .Select(PadRowTo26)
                    .ToList<IList<object>>();

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

        _logger.LogInformation("Google Sheet 同步完成");
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
