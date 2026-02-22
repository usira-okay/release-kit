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
/// 從 Redis 讀取整合後的 Release 資料，比對 Google Sheet 現有內容後，
/// 新增缺少的列或更新現有列的 Authors / PullRequestUrls 欄位，並對各專案區塊排序。
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
        _logger.LogInformation("開始更新 Google Sheets");

        // 1. 從 Redis 讀取整合資料
        var json = await _redisService.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("缺少整合資料：Redis Hash '{Hash}:{Field}' 無有效資料，略過 Google Sheets 更新",
                RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
            return;
        }

        var result = json.ToTypedObject<ConsolidatedReleaseResult>();
        if (result == null || result.Projects.Count == 0)
        {
            _logger.LogWarning("整合資料為空或反序列化失敗，略過 Google Sheets 更新");
            return;
        }

        var opts = _options.Value;
        var mapping = opts.ColumnMapping;

        // 2. 驗證欄位設定（每個欄位必須是 A-Z 單一字母）
        ValidateColumnMapping(mapping);

        // 3. 讀取 Google Sheet 資料
        var sheetData = await _googleSheetService.GetSheetDataAsync(opts.SpreadsheetId, opts.SheetName, "A:Z");
        if (sheetData == null)
        {
            _logger.LogWarning("Google Sheet '{SheetName}' 讀取失敗或無資料，略過更新", opts.SheetName);
            return;
        }

        // 4. 取得工作表 ID（InsertRowAsync 需要數字 ID）
        var sheetId = await _googleSheetService.GetSheetIdAsync(opts.SpreadsheetId, opts.SheetName);

        // 5. 以可變 List 操作 sheetData，追蹤插入後的位移
        var rows = sheetData.Select(r => r.ToList()).ToList();
        var repoColIdx = ColumnLetterToIndex(mapping.RepositoryNameColumn);
        var ukColIdx = ColumnLetterToIndex(mapping.UniqueKeyColumn);

        // 6. 依序處理每個專案
        foreach (var (projectName, entries) in result.Projects)
        {
            await ProcessProjectAsync(projectName, entries, rows, sheetId, opts, repoColIdx, ukColIdx);
        }

        _logger.LogInformation("Google Sheets 更新完成，共處理 {ProjectCount} 個專案", result.Projects.Count);
    }

    /// <summary>
    /// 處理單一專案的 Google Sheet 更新（新增或更新列，並排序）
    /// </summary>
    private async Task ProcessProjectAsync(
        string projectName,
        List<ConsolidatedReleaseEntry> entries,
        List<List<object>> rows,
        int sheetId,
        GoogleSheetOptions opts,
        int repoColIdx,
        int ukColIdx)
    {
        var mapping = opts.ColumnMapping;

        // 找出專案標題列與下一個專案標題列的索引（定義資料區段邊界）
        int headerRowIdx = -1;
        int nextHeaderRowIdx = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            var val = GetCellValue(rows[i], repoColIdx);
            if (val == projectName && headerRowIdx < 0)
            {
                headerRowIdx = i;
            }
            else if (headerRowIdx >= 0 && !string.IsNullOrEmpty(val))
            {
                nextHeaderRowIdx = i;
                break;
            }
        }

        if (headerRowIdx < 0)
        {
            _logger.LogWarning("工作表中找不到專案 '{ProjectName}' 的標題列，略過此專案", projectName);
            return;
        }

        // 建立 UniqueKey → 列索引 對映（0-based）
        var ukToRowIdx = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = headerRowIdx + 1; i < nextHeaderRowIdx; i++)
        {
            var uk = GetCellValue(rows[i], ukColIdx);
            if (!string.IsNullOrEmpty(uk))
                ukToRowIdx[uk] = i;
        }

        // 處理每筆整合記錄
        foreach (var entry in entries)
        {
            var uk = $"{entry.WorkItemId}{projectName}";

            if (ukToRowIdx.TryGetValue(uk, out var existingRowIdx))
            {
                // 既有列：僅更新 Authors 與 PullRequestUrls
                var rowNum = existingRowIdx + 1; // Google Sheets 列號為 1-based
                var updates = new Dictionary<string, string>
                {
                    [$"{mapping.AuthorsColumn}{rowNum}"] = FormatAuthors(entry),
                    [$"{mapping.PullRequestUrlsColumn}{rowNum}"] = FormatPrUrls(entry)
                };
                await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);

                // 同步更新記憶體中的列資料
                EnsureRowCapacity(rows[existingRowIdx], 26);
                rows[existingRowIdx][ColumnLetterToIndex(mapping.AuthorsColumn)] = FormatAuthors(entry);
                rows[existingRowIdx][ColumnLetterToIndex(mapping.PullRequestUrlsColumn)] = FormatPrUrls(entry);
            }
            else
            {
                // 新列：在專案區段末尾插入空白列後寫入所有欄位
                await _googleSheetService.InsertRowAsync(opts.SpreadsheetId, sheetId, nextHeaderRowIdx);

                var rowNum = nextHeaderRowIdx + 1; // Google Sheets 列號為 1-based
                var updates = BuildNewRowCells(entry, projectName, rowNum, mapping);
                await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);

                // 更新記憶體狀態
                var newRow = BuildInMemoryRow(entry, projectName, mapping);
                rows.Insert(nextHeaderRowIdx, newRow);
                ukToRowIdx[uk] = nextHeaderRowIdx;
                nextHeaderRowIdx++;
            }
        }

        // 對專案資料區段進行排序
        await SortProjectBlockAsync(headerRowIdx, nextHeaderRowIdx, rows, opts);
    }

    /// <summary>
    /// 對指定專案資料區段的列進行排序，並將差異寫回 Google Sheet
    /// </summary>
    private async Task SortProjectBlockAsync(
        int headerRowIdx,
        int nextHeaderRowIdx,
        List<List<object>> rows,
        GoogleSheetOptions opts)
    {
        var dataRowCount = nextHeaderRowIdx - headerRowIdx - 1;
        if (dataRowCount <= 1) return;

        var mapping = opts.ColumnMapping;
        var teamColIdx = ColumnLetterToIndex(mapping.TeamColumn);
        var authorsColIdx = ColumnLetterToIndex(mapping.AuthorsColumn);
        var featureColIdx = ColumnLetterToIndex(mapping.FeatureColumn);
        var ukColIdx = ColumnLetterToIndex(mapping.UniqueKeyColumn);

        // 取得資料列的副本（保留原始參考用於比對）
        var dataRows = rows.GetRange(headerRowIdx + 1, dataRowCount);

        // 依 TeamColumn → AuthorsColumn → FeatureColumn → UniqueKeyColumn 排序，空白值排最後
        var sorted = dataRows
            .OrderBy(r => GetSortKey(r, teamColIdx), StringComparer.Ordinal)
            .ThenBy(r => GetSortKey(r, authorsColIdx), StringComparer.Ordinal)
            .ThenBy(r => GetSortKey(r, featureColIdx), StringComparer.Ordinal)
            .ThenBy(r => GetSortKey(r, ukColIdx), StringComparer.Ordinal)
            .ToList();

        // 若排序前後順序相同，略過寫回動作
        var changed = false;
        for (int i = 0; i < dataRows.Count; i++)
        {
            if (!ReferenceEquals(dataRows[i], sorted[i]))
            {
                changed = true;
                break;
            }
        }
        if (!changed) return;

        // 建立需要寫回的儲存格更新（僅寫入與原始位置值不同的儲存格）
        var updates = new Dictionary<string, string>();
        for (int rowOffset = 0; rowOffset < sorted.Count; rowOffset++)
        {
            if (ReferenceEquals(sorted[rowOffset], dataRows[rowOffset])) continue;

            var sheetRowNum = headerRowIdx + rowOffset + 2; // 1-based（標題列佔 headerRowIdx+1）
            var sortedRow = sorted[rowOffset];
            var originalRow = dataRows[rowOffset];

            for (int col = 0; col < 26; col++)
            {
                var newVal = col < sortedRow.Count ? sortedRow[col]?.ToString() ?? string.Empty : string.Empty;
                var oldVal = col < originalRow.Count ? originalRow[col]?.ToString() ?? string.Empty : string.Empty;
                if (newVal != oldVal)
                {
                    updates[$"{(char)('A' + col)}{sheetRowNum}"] = newVal;
                }
            }
        }

        if (updates.Count > 0)
        {
            await _googleSheetService.UpdateCellsAsync(opts.SpreadsheetId, opts.SheetName, updates);
        }

        // 同步更新記憶體中的排序結果
        for (int i = 0; i < sorted.Count; i++)
        {
            rows[headerRowIdx + 1 + i] = sorted[i];
        }
    }

    /// <summary>
    /// 建立新列的儲存格更新字典（包含所有欄位）
    /// </summary>
    private static Dictionary<string, string> BuildNewRowCells(
        ConsolidatedReleaseEntry entry,
        string projectName,
        int rowNum,
        ColumnMappingOptions mapping)
    {
        return new Dictionary<string, string>
        {
            [$"{mapping.FeatureColumn}{rowNum}"] = FormatFeature(entry),
            [$"{mapping.TeamColumn}{rowNum}"] = entry.TeamDisplayName,
            [$"{mapping.AuthorsColumn}{rowNum}"] = FormatAuthors(entry),
            [$"{mapping.PullRequestUrlsColumn}{rowNum}"] = FormatPrUrls(entry),
            [$"{mapping.UniqueKeyColumn}{rowNum}"] = $"{entry.WorkItemId}{projectName}",
            [$"{mapping.AutoSyncColumn}{rowNum}"] = "TRUE"
        };
    }

    /// <summary>
    /// 建立新列的記憶體表示（26 欄，A-Z）
    /// </summary>
    private static List<object> BuildInMemoryRow(
        ConsolidatedReleaseEntry entry,
        string projectName,
        ColumnMappingOptions mapping)
    {
        var row = Enumerable.Range(0, 26).Select(_ => (object)string.Empty).ToList();
        row[ColumnLetterToIndex(mapping.FeatureColumn)] = FormatFeature(entry);
        row[ColumnLetterToIndex(mapping.TeamColumn)] = entry.TeamDisplayName;
        row[ColumnLetterToIndex(mapping.AuthorsColumn)] = FormatAuthors(entry);
        row[ColumnLetterToIndex(mapping.PullRequestUrlsColumn)] = FormatPrUrls(entry);
        row[ColumnLetterToIndex(mapping.UniqueKeyColumn)] = $"{entry.WorkItemId}{projectName}";
        row[ColumnLetterToIndex(mapping.AutoSyncColumn)] = "TRUE";
        return row;
    }

    /// <summary>
    /// 確保列的容量至少達到指定欄數
    /// </summary>
    private static void EnsureRowCapacity(List<object> row, int minCount)
    {
        while (row.Count < minCount)
            row.Add(string.Empty);
    }

    /// <summary>
    /// 格式化 Feature 欄位：以 HYPERLINK 公式呈現 VSTS{workItemId} - {title}
    /// </summary>
    private static string FormatFeature(ConsolidatedReleaseEntry entry)
    {
        var display = $"VSTS{entry.WorkItemId} - {entry.Title}";
        if (!string.IsNullOrEmpty(entry.WorkItemUrl))
            return $"=HYPERLINK(\"{entry.WorkItemUrl}\",\"{display}\")";
        return display;
    }

    /// <summary>
    /// 格式化作者清單：依 authorName 排序後以換行符號分隔
    /// </summary>
    private static string FormatAuthors(ConsolidatedReleaseEntry entry) =>
        string.Join("\n", entry.Authors
            .Select(a => a.AuthorName)
            .OrderBy(n => n, StringComparer.Ordinal));

    /// <summary>
    /// 格式化 PR URL 清單：依 url 排序後以換行符號分隔
    /// </summary>
    private static string FormatPrUrls(ConsolidatedReleaseEntry entry)
    {
        if (entry.PullRequests.Count == 0) return string.Empty;
        return string.Join("\n", entry.PullRequests
            .Select(p => p.Url)
            .OrderBy(u => u, StringComparer.Ordinal));
    }

    /// <summary>
    /// 取得排序用的鍵值（空白值轉為 Unicode 最大值，使其排在最後）
    /// </summary>
    private static string GetSortKey(List<object> row, int colIdx)
    {
        var val = colIdx < row.Count ? row[colIdx]?.ToString() : null;
        return string.IsNullOrEmpty(val) ? "\uFFFF" : val;
    }

    /// <summary>
    /// 取得指定列中指定欄的儲存格值（超出範圍時回傳空字串）
    /// </summary>
    private static string GetCellValue(List<object> row, int colIdx) =>
        colIdx < row.Count ? row[colIdx]?.ToString() ?? string.Empty : string.Empty;

    /// <summary>
    /// 將欄位字母轉換為 0-based 索引（A=0, B=1, ...）
    /// </summary>
    private static int ColumnLetterToIndex(string col) => col[0] - 'A';

    /// <summary>
    /// 驗證欄位映射設定中每個欄位字母是否均為有效的 A-Z 單一字母
    /// </summary>
    private static void ValidateColumnMapping(ColumnMappingOptions mapping)
    {
        ValidateColumnLetter(nameof(mapping.RepositoryNameColumn), mapping.RepositoryNameColumn);
        ValidateColumnLetter(nameof(mapping.FeatureColumn), mapping.FeatureColumn);
        ValidateColumnLetter(nameof(mapping.TeamColumn), mapping.TeamColumn);
        ValidateColumnLetter(nameof(mapping.AuthorsColumn), mapping.AuthorsColumn);
        ValidateColumnLetter(nameof(mapping.PullRequestUrlsColumn), mapping.PullRequestUrlsColumn);
        ValidateColumnLetter(nameof(mapping.UniqueKeyColumn), mapping.UniqueKeyColumn);
        ValidateColumnLetter(nameof(mapping.AutoSyncColumn), mapping.AutoSyncColumn);
    }

    /// <summary>
    /// 驗證欄位字母設定是否為有效的 A-Z 單一字母
    /// </summary>
    private static void ValidateColumnLetter(string columnName, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 1 || value[0] < 'A' || value[0] > 'Z')
            throw new InvalidOperationException(
                $"欄位設定 '{columnName}' 的值 '{value}' 不是有效的 A-Z 單一字母");
    }
}

