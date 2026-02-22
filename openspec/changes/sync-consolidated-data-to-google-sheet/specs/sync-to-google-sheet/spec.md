## ADDED Requirements

### Requirement: ConsolidateReleaseDataTask 無資料時優雅結束

當 `ConsolidateReleaseDataTask` 執行時，若 Redis 中缺少前置 PR 資料或 Work Item 資料，任務 SHALL 記錄警告訊息並正常結束（`return`），不得拋出例外。

#### Scenario: 缺少 PR 資料時優雅結束
- **WHEN** Redis 中 Bitbucket 與 GitLab 的 `PullRequestsByUser` 均無有效 PR 資料
- **THEN** 任務記錄 Warning 等級日誌並正常結束，不拋出 `InvalidOperationException`

#### Scenario: 缺少 Work Item 資料時優雅結束
- **WHEN** Redis 中 AzureDevOps 的 `WorkItemsUserStories` 無有效資料
- **THEN** 任務記錄 Warning 等級日誌並正常結束，不拋出 `InvalidOperationException`

### Requirement: 從 Redis 讀取整合資料

`UpdateGoogleSheetsTask` SHALL 從 Redis Hash `ReleaseData:Consolidated` 讀取 `ConsolidatedReleaseResult` 資料。

#### Scenario: Redis 中無整合資料
- **WHEN** Redis Hash `ReleaseData:Consolidated` 欄位不存在或為空
- **THEN** 任務記錄 Warning 日誌並正常結束，不拋出例外

#### Scenario: Redis 中有整合資料
- **WHEN** Redis Hash `ReleaseData:Consolidated` 欄位存在且包含有效 JSON
- **THEN** 成功反序列化為 `ConsolidatedReleaseResult` 並繼續後續流程

### Requirement: 驗證 Google Sheet 連線與讀取

任務 SHALL 在同步前驗證是否可連線至 Google Sheet 並讀取 A-Z 範圍的所有資料。

#### Scenario: 成功讀取 Google Sheet 資料
- **WHEN** Google Sheet 連線成功且可讀取 `A:Z` 範圍資料
- **THEN** 取得 Sheet 中所有資料繼續後續流程

#### Scenario: 無法連線或讀取 Google Sheet
- **WHEN** Google Sheet 連線失敗或讀取操作失敗
- **THEN** 任務記錄 Warning 日誌並正常結束，不拋出例外

#### Scenario: ColumnMapping 設定欄位超過 Z
- **WHEN** `GoogleSheet:ColumnMapping` 中任一欄位設定值超過單字母 A-Z 範圍（如 "AA"）
- **THEN** 任務 SHALL 拋出 `InvalidOperationException`，錯誤訊息明確指出哪個欄位設定無效

### Requirement: 以 UniqueKey 判斷新增或更新

任務 SHALL 使用 `$"{workItemId}{dictionaryKey}"` 作為 UniqueKey（UK），與 Google Sheet 中 UniqueKeyColumn 的既有值比對，決定新增或更新。

#### Scenario: UniqueKey 不存在於 Sheet 中
- **WHEN** 某筆 FEATURE_DATA 的 UK 在 Sheet 的 UniqueKeyColumn 中找不到相同值
- **THEN** 執行新增資料流程

#### Scenario: UniqueKey 已存在於 Sheet 中
- **WHEN** 某筆 FEATURE_DATA 的 UK 在 Sheet 的 UniqueKeyColumn 中找到相同值
- **THEN** 執行更新資料流程

### Requirement: 新增資料時在正確位置插入列

新增資料時，任務 SHALL 根據 RepositoryNameColumn 的值定位 Project 區塊，在該區塊內插入新列。

#### Scenario: 在第一個 Project 區塊後插入
- **WHEN** PROJECT_NAME 對應到 RepositoryNameColumn 的第一個值（如 Z1="repo1"，下一個 RepositoryNameColumn 是 Z2="repo2"）
- **THEN** 在 Z1 之後插入新列（原 Z2 及後續資料往下移動）

#### Scenario: 在中間 Project 區塊前插入
- **WHEN** PROJECT_NAME 對應到 RepositoryNameColumn 的非最後一個值（如 Z6="repo3"，下一個 RepositoryNameColumn 是 Z10="repo4"）
- **THEN** 在下一個 RepositoryNameColumn 之前插入新列（如在 row 7 位置插入，原 row 7 及後續資料往下移動）

#### Scenario: 在最後一個 Project 區塊後插入
- **WHEN** PROJECT_NAME 對應到 RepositoryNameColumn 的最後一個值（如 Z10="repo4"，之後無其他 RepositoryNameColumn）
- **THEN** 在該 RepositoryNameColumn 行之後插入新列

### Requirement: 新增資料時填入正確欄位值

新增資料時，任務 SHALL 在插入的新列中填入以下欄位值。

#### Scenario: 填入 Feature 欄位（含超連結）
- **WHEN** 新增一筆 FEATURE_DATA
- **THEN** FeatureColumn 填入 `VSTS{workItemId} - {title}`，並包含指向 `workItemUrl` 的超連結

#### Scenario: 填入 Team 欄位
- **WHEN** 新增一筆 FEATURE_DATA
- **THEN** TeamColumn 填入 `teamDisplayName`

#### Scenario: 填入 Authors 欄位
- **WHEN** 新增一筆 FEATURE_DATA 且有多個 Authors
- **THEN** AuthorsColumn 填入所有 `authorName`，先依 `authorName` 排序後以換行符號（`\n`）分隔

#### Scenario: 填入 PullRequestUrls 欄位
- **WHEN** 新增一筆 FEATURE_DATA 且有多個 PullRequests
- **THEN** PullRequestUrlsColumn 填入所有 PR `url`，先依 `url` 排序後以換行符號（`\n`）分隔

#### Scenario: 填入 UniqueKey 欄位
- **WHEN** 新增一筆 FEATURE_DATA
- **THEN** UniqueKeyColumn 填入 `$"{workItemId}{dictionaryKey}"`

#### Scenario: 填入 AutoSync 欄位
- **WHEN** 新增一筆 FEATURE_DATA
- **THEN** AutoSyncColumn 固定填入 `TRUE`

### Requirement: 更新資料時僅更新指定欄位

更新資料時，任務 SHALL 僅更新 AuthorsColumn 與 PullRequestUrlsColumn。

#### Scenario: 更新 Authors 欄位
- **WHEN** UK 已存在於 Sheet 中且 FEATURE_DATA 有 Authors 資料
- **THEN** 僅更新該列的 AuthorsColumn，內容為所有 `authorName` 依排序後以換行符號分隔

#### Scenario: 更新 PullRequestUrls 欄位
- **WHEN** UK 已存在於 Sheet 中且 FEATURE_DATA 有 PullRequests 資料
- **THEN** 僅更新該列的 PullRequestUrlsColumn，內容為所有 `url` 依排序後以換行符號分隔

#### Scenario: 不更新其他欄位
- **WHEN** UK 已存在於 Sheet 中
- **THEN** FeatureColumn、TeamColumn、UniqueKeyColumn、AutoSyncColumn、RepositoryNameColumn 均不被修改

### Requirement: Project 區塊內排序

當任何 Project 區塊有新增或更新資料時，任務 SHALL 對該 Project 區塊內的非標記列進行排序。

#### Scenario: 依多欄位排序
- **WHEN** Project 區塊內有資料需要排序
- **THEN** 依序以 TeamColumn、AuthorsColumn、FeatureColumn、UniqueKeyColumn 升冪排序

#### Scenario: 空白值排最後
- **WHEN** 排序欄位中有空白值
- **THEN** 空白值排在非空白值之後

#### Scenario: 排序範圍限定
- **WHEN** PROJECT_NAME = "repo3" 且 RepositoryNameColumn 在 row 6，下一個 RepositoryNameColumn 在 row 10
- **THEN** 僅對 row 7 至 row 9 之間的資料進行排序，不影響 row 6 與 row 10 的 RepositoryNameColumn 標記列

### Requirement: Google Sheet 服務抽象介面

系統 SHALL 提供 `IGoogleSheetService` 介面，定義與 Google Sheets API 互動的所有操作。

#### Scenario: 介面定義於 Domain 層
- **WHEN** Application 層的 Task 需要存取 Google Sheet
- **THEN** 透過 `IGoogleSheetService` 介面注入，不直接依賴 Infrastructure 層實作

#### Scenario: 實作位於 Infrastructure 層
- **WHEN** 系統需要實際呼叫 Google Sheets API
- **THEN** `GoogleSheetService` 實作位於 `ReleaseKit.Infrastructure.GoogleSheets` 命名空間
