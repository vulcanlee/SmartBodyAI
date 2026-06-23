# API 端點參考（SmartAgentApi）

後端 `SmartAgentApi` 提供 DICOM 上傳與 AI 結果查詢／下載端點。前端 `SmartBodyAI` 透過 `InferenceHostApi`（預設 `https://localhost:7274`）呼叫。

> 來源：`source/SmartAgentApi/SmartAgentApi/Controllers/DicomPackController.cs`
> 控制器路由前綴：`/DicomPack`（`[Route("[controller]")]`）

---

## 1. 上傳 DICOM ZIP

```
POST /DicomPack
Content-Type: multipart/form-data
```

| 項目 | 說明 |
|------|------|
| 表單欄位 | `file`（`IFormFile`），必須為 `.zip` |
| 大小上限 | 1,000,000,000 bytes（約 1 GB，`RequestSizeLimit`） |

**行為**：驗證副檔名為 `.zip` → 以 `yyyyMMddHHmmssfff_{Guid:N}` 產生 `workId` 建立獨立工作目錄 → 解壓 → 呼叫 `PushToAICheck` 推送 AI 流程。

**回應**

| 狀態 | 內容 |
|------|------|
| `200 OK` | `{ "Message": "上傳並解壓縮成功。", "WorkId": "<workId>", "ExtractPath": "<path>" }` |
| `400 Bad Request` | 未收到檔案／檔案為空／非 `.zip`／非有效 ZIP 格式 |
| `500 Internal Server Error` | `{ "Message": "處理 ZIP 檔案時發生錯誤。", "Error": "<訊息>" }` |

> ZIP 內須包含 `PatientData.json` 與 `L3CT.dicm`（`PushToAICheck` 依此處理）。

---

## 2. 查詢 AI 結果狀態

```
GET /DicomPack/CheckResult/{checkKey}
```

| 參數 | 說明 |
|------|------|
| `checkKey` | 對應完成佇列下的工作目錄名稱（病人識別碼 / KeyName） |

**行為**：檢查 `{QueueFolderPath}/{CompleteQueueName}/{checkKey}` 是否存在，且子目錄涵蓋 `Phase1Result`、`Phase2Result`、`Phase3Result`。

**回應**

| 狀態 | 內容 |
|------|------|
| `200 OK` | `{ "Status": true, "Message": "AI 推論已完成，結果可供下載。" }` |
| `404 Not Found` | `{ "Status": false, "Message": "尚未完成 AI 推論，請稍後再試。" }` |

---

## 3. 下載 AI 結果 ZIP

```
GET /DicomPack/Download/{checkKey}
```

| 參數 | 說明 |
|------|------|
| `checkKey` | 同上 |

**行為**：讀取 `PatientData.json` 與 AI 推論 CSV（`Phase3Result/input.csv`），計算身體組成指標（SMD、IMAT、LAMA、NAMA、SMA、SMI、Myosteatosis），寫入 `BodyAIResult.json`，將結果目錄壓成 ZIP 回傳。

**回應**

| 狀態 | 內容 |
|------|------|
| `200 OK` | `application/zip` 檔案下載（檔名 `{checkKey}.zip`） |
| `404 Not Found` | `{ "Status": false, "Message": "尚未完成 AI 推論，請稍後再試。" }`（結果目錄不存在） |

---

## 串接流程

```
POST /DicomPack ──▶ 取得 WorkId
   │
   ▼ （輪詢）
GET /DicomPack/CheckResult/{checkKey} ──▶ Status=true
   │
   ▼
GET /DicomPack/Download/{checkKey} ──▶ 下載結果 ZIP
```

完整時序見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md) 第 3.2 節。

> 注意：開發環境後端對外部呼叫略過 SSL 憑證驗證（見 CHANGELOG 2026/02）；正式環境須改回驗證。
