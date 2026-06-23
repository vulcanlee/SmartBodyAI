# 設定參數參考表（Configuration Reference）

本文件列出 SmartBodyAI 前端與 SmartAgentApi 後端的設定鍵。修改設定前請對照本表，避免改錯影響流程。
機密管理與環境變數注入見 [`../operations/production-environment-variable-sop.md`](../operations/production-environment-variable-sop.md)、約束見 [`../guidelines/constraints.md`](../guidelines/constraints.md)。

> **環境變數覆寫**：ASP.NET Core 以雙底線對應設定階層。例：`SmartAppSetting:ClientSecret` → `SmartAppSetting__ClientSecret`。

---

## 一、前端 SmartBodyAI（`SmartAppSetting`）

檔案：`source/SmartBodyAI/SmartBodyAI/appsettings.json`

| 鍵 | 說明 | 開發預設值 |
|----|------|-----------|
| `SystemInformation:SystemName` | 系統名稱 | `身體組成 SMART App` |
| `SystemInformation:SystemDescription` | 系統描述 | `身體組成 SMART App` |
| `SystemInformation:SystemVersion` | 版本號（每次建置 Patch +1，見 [versioning](../guidelines/versioning.md)） | `1.2.36 (2026/04/06)` |
| `FhirServerUrl` | FHIR 伺服器基底 URL（SMART discovery 起點） | smarthealthit 公開 sandbox（另備 `launcher.bdlfhir.net` 註解） |
| `RedirectUrl` | OAuth callback 目標，**須與授權伺服器登記值完全一致** | `https://localhost:7170/patient-information` |
| `InferenceHostApi` | 後端 SmartAgentApi 位址 | `https://localhost:7274` |
| `ClientId` | SMART client_id | `smart-app` |
| `ClientSecret` | client 機密，**留空，由環境變數／User Secrets 注入** | `""` |
| `AuthorizationScope` | SMART 授權 scope | `openid fhirUser profile launch/patient patient/*.read patient/Encounter.read patient/MedicationRequest.read patient/ServiceRequest.read user/*.read` |
| `ProcessDelayTimeInMilliSeconds` | 流程延遲（毫秒） | `1000` |
| `UploadDicomTempPath` | DICOM 上傳暫存目錄 | `C:\temp\SmartBodyAI\TempUploads` |
| `UploadDicomPath` | DICOM 上傳目錄 | `C:\temp\SmartBodyAI\Uploads` |
| `DicomImagePath` | DICOM 轉圖輸出目錄 | `C:\temp\SmartBodyAI\Images` |
| `AIResultPath` | AI 結果目錄 | `C:\temp\temp` |

> 對應模型：`Models/SettingModel.cs`、`Models/SmartAppSettingModel.cs`、`Models/SystemInformationModel.cs`。
> `IsDebug` 可由查詢參數 `?debug=true` 覆寫；`CodeChallengeMethod` 固定 `S256`（見 [ADR 0002](../architecture/adr/0002-pkce-s256-mandatory.md)）。

---

## 二、後端 SmartAgentApi（`AgentSetting`）

檔案：`source/SmartAgentApi/SmartAgentApi/appsettings.json`

| 鍵 | 說明 | 開發預設值 |
|----|------|-----------|
| `DicomFolderPath` | DICOM 根目錄 | `C:\temp\Dicom` |
| `QueueFolderPath` | 佇列根目錄 | `C:\temp\CTMS\Queue` |
| `InBoundQueueName` | 進站佇列名稱 | `InBound` |
| `Phase1QueueName` / `Phase1WaitingQueueName` / `Phase1TmpFolder` | 第一階段佇列／等待佇列／暫存 | `Phase1` / `Phase1Waiting` / `C:\CanWellBeing\temp1` |
| `Phase2QueueName` / `Phase2WaitingQueueName` / `Phase2TmpFolder` | 第二階段 | `Phase2` / `Phase2Waiting` / `C:\CanWellBeing\temp2` |
| `Phase3QueueName` / `Phase3WaitingQueueName` | 第三階段 | `Phase3` / `Phase3Waiting` |
| `OutBoundQueueName` | 出站佇列名稱 | `OutBound` |
| `CompleteQueueName` | 完成佇列名稱（結果查詢／下載依此目錄） | `Complete` |
| `InferencePath` | 推論程式路徑 | `C:\CanWellBeing\inference` |
| `風險評估模型` | 子宮內膜癌模型路徑 | `C:\EndometrioidCancer` |
| `風險評估模型OC` | 卵巢癌模型路徑 | `C:\EndometrioidCancer\OvarianCancer` |

> 後端依賴外部 `CTMS` 專案（見 [ADR 0005](../architecture/adr/0005-external-ctms-dependency.md)）。
> 注意：`DicomPackController.Upload` 目前以硬編碼路徑 `C:\temp\SmartBodyAI\TempUploads\DicomPacks` 作為上傳解壓基底。

---

## 三、日誌（NLog）

| 項目 | 前端 | 後端 |
|------|------|------|
| 設定檔 | `nlog.config` | `nlog.config` |
| 日誌路徑 | `C:/temp/logs/SmartBodyAI-logfile-${shortdate}.log` | `C:/temp/logs/SmartAgentApi-logfile-${shortdate}.log` |
| 保留 | 30 天、歸檔 100MB、壓縮 | 同左 |
