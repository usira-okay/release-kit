# Docker 使用指南

本文件說明如何使用 Docker 與 Docker Compose 執行 Release-Kit 應用程式。

## 必要條件

- Docker Engine 20.10 或更新版本
- Docker Compose v2.0 或更新版本

## 架構概覽

Docker Compose 環境包含以下服務：

1. **Seq** - 結構化日誌伺服器，用於收集與查詢應用程式日誌
2. **Redis** - 快取與資料存儲服務
3. **Release-Kit** - 主要 Console 應用程式

## 快速開始

### 1. 啟動所有服務

```bash
docker-compose up -d
```

此指令會：
- 啟動 Seq 日誌服務（Port 5341）
- 啟動 Redis 服務（Port 6379）
- 建置並啟動 Release-Kit Console 應用程式

### 2. 查看日誌

查看所有服務的日誌：
```bash
docker-compose logs -f
```

只查看 Release-Kit 應用程式日誌：
```bash
docker-compose logs -f release-kit
```

### 3. 停止服務

```bash
docker-compose down
```

保留資料卷（Redis 與 Seq 資料）：
```bash
docker-compose down
```

完全清除（包含資料卷）：
```bash
docker-compose down -v
```

## 服務說明

### Seq 日誌服務

- **存取位址**: http://localhost:5341
- **用途**: 查看與搜尋應用程式日誌
- **資料持久化**: 透過 Docker Volume `seq-data` 儲存

登入 Seq Web UI 即可查看即時日誌、進行結構化查詢與設定告警。

### Redis 快取服務

- **連線位址**: localhost:6379
- **用途**: 快取與資料存儲
- **資料持久化**: 透過 Docker Volume `redis-data` 與 AOF (Append-Only File) 持久化

#### 使用 Redis CLI 連接

```bash
docker exec -it release-kit-redis redis-cli
```

查看所有鍵值：
```redis
KEYS *
```

查看特定鍵值：
```redis
GET ReleaseKit:test:hello
```

### Release-Kit 應用程式

應用程式會自動執行以下測試：
1. 載入組態設定
2. 連接至 Redis
3. 執行 Redis 讀寫測試
4. 將日誌輸出至 Console 與 Seq

## 環境變數配置

可透過環境變數覆寫組態設定。編輯 `docker-compose.yml` 中的 `environment` 區塊：

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - Redis__ConnectionString=redis:6379
  - Redis__InstanceName=ReleaseKit:
  - Seq__ServerUrl=http://seq:80
  - Seq__ApiKey=your-api-key
```

### 可用的環境變數

| 變數名稱 | 預設值 | 說明 |
|---------|-------|------|
| `ASPNETCORE_ENVIRONMENT` | Development | 執行環境 (Development, Qa, Production) |
| `Redis__ConnectionString` | redis:6379 | Redis 連線字串 |
| `Redis__InstanceName` | ReleaseKit: | Redis 鍵值前綴 |
| `Seq__ServerUrl` | http://seq:80 | Seq 伺服器位址 |
| `Seq__ApiKey` | (空) | Seq API 金鑰（可選） |

## 自訂組態檔

如需使用自訂組態檔，可修改 `docker-compose.yml` 中的 volumes 設定：

```yaml
volumes:
  - ./your-custom-settings.json:/app/appsettings.Development.json:ro
```

## 開發模式

### 僅啟動基礎設施服務

如果要在本機執行 Release-Kit，但使用 Docker 的 Seq 與 Redis：

```bash
docker-compose up -d seq redis
```

然後在本機執行：

```bash
cd src/ReleaseKit.Console
dotnet run
```

### 重新建置應用程式映像

當程式碼變更後，需要重新建置 Docker 映像：

```bash
docker-compose build release-kit
docker-compose up -d release-kit
```

或一次完成：
```bash
docker-compose up -d --build
```

## 常見問題

### Q: 如何清除所有 Redis 資料？

```bash
docker exec -it release-kit-redis redis-cli FLUSHALL
```

### Q: 如何備份 Redis 資料？

Redis 已啟用 AOF 持久化，資料會自動儲存。若需手動備份：

```bash
docker exec release-kit-redis redis-cli BGSAVE
docker cp release-kit-redis:/data/dump.rdb ./backup-$(date +%Y%m%d).rdb
```

### Q: 如何重設 Seq 日誌？

最簡單的方式是刪除 Seq 資料卷：

```bash
docker-compose down
docker volume rm release-kit_seq-data
docker-compose up -d
```

### Q: 應用程式無法連接至 Redis/Seq？

確認服務已啟動：
```bash
docker-compose ps
```

檢查服務日誌：
```bash
docker-compose logs seq
docker-compose logs redis
```

### Q: 如何變更 Redis 或 Seq 的 Port？

編輯 `docker-compose.yml` 中的 ports 設定：

```yaml
services:
  redis:
    ports:
      - "6380:6379"  # 將本機 6380 對應至容器 6379
  
  seq:
    ports:
      - "5342:80"    # 將本機 5342 對應至容器 80
```

## 生產環境建議

1. **啟用 Seq API Key**: 在 Seq Web UI 中建立 API Key，並在環境變數中設定
2. **設定 Redis 密碼**: 修改 Redis 設定啟用認證
3. **資料卷備份**: 定期備份 `seq-data` 與 `redis-data` 資料卷
4. **資源限制**: 在 `docker-compose.yml` 中加入 CPU 與記憶體限制
5. **網路隔離**: 使用自訂網路與防火牆規則限制存取

範例資源限制設定：

```yaml
services:
  redis:
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
```

## 進階設定

### 使用外部 Redis

如果要連接至現有的 Redis 實例，移除 docker-compose.yml 中的 redis 服務，並修改環境變數：

```yaml
environment:
  - Redis__ConnectionString=your-redis-host:6379
```

### 使用外部 Seq

類似地，可移除 seq 服務並指向外部 Seq 伺服器：

```yaml
environment:
  - Seq__ServerUrl=https://your-seq-server.com
  - Seq__ApiKey=your-api-key
```

## 疑難排解

啟用詳細日誌：

```yaml
environment:
  - Logging__LogLevel__Default=Debug
```

進入容器進行偵錯：

```bash
docker exec -it release-kit-app bash
```

## 相關文件

- [Seq 官方文件](https://docs.datalust.co/docs)
- [Redis 官方文件](https://redis.io/documentation)
- [Docker Compose 參考](https://docs.docker.com/compose/)
