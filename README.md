# SmartBodyAI

SmartBodyAI 是一個結合 `SMART on FHIR`、`DICOM` 與 AI 推論流程的醫療應用示範專案。  
此專案使用 `ASP.NET Core Blazor Server` 建立前端互動流程，並透過獨立的 API 專案處理 DICOM 壓縮包上傳、AI 推論整合與結果下載。

## 專案簡介

本專案的主要目標，是從 `SMART on FHIR` sandbox 或 launcher 啟動應用，完成授權流程後讀取病患相關 FHIR 資源，並提供 DICOM 影像上傳與 AI 分析結果查詢能力。

整體架構不是單一網站，而是由前端主站與後端 API 組成：

- `SmartBodyAI`：Blazor Server 主站，負責 SMART 啟動、病患資料呈現、健康檢查與 DICOM 操作流程
- `SmartAgentApi`：後端 API，負責接收 DICOM ZIP、解壓、推送 AI 流程、查詢與下載分析結果

## 主要功能

- 支援 `SMART on FHIR` standalone launch
- 支援 SMART discovery、OAuth2 Authorization Code Flow、PKCE 與 OIDC
- 讀取病患基本資料與相關 FHIR 資源
- 上傳 DICOM 壓縮包並查詢、下載 AI 分析結果
- 提供 `SMART readiness / health check` 頁面
- 提供 `SMART checklist` 頁面，協助人工檢查授權與啟動條件

## 系統架構

### 主要專案

- `source/SmartBodyAI/SmartBodyAI`
  - `Blazor Server` 主站
  - 預設開發網址：`https://localhost:7170`
  - 首頁會進入 SMART 啟動流程
  - 重要頁面包含：
    - `/patient-information`：SMART callback 與病患資料頁
    - `/health-check`：SMART readiness 診斷頁
    - `/smart-checklist`：SMART 檢查清單頁

- `source/SmartAgentApi/SmartAgentApi`
  - DICOM 與 AI 整合 API
  - 預設開發網址：`https://localhost:7274`
  - 重要端點包含：
    - `POST /DicomPack`：上傳 DICOM ZIP
    - `GET /DicomPack/CheckResult/{checkKey}`：查詢 AI 結果是否完成
    - `GET /DicomPack/Download/{checkKey}`：下載整合後結果 ZIP

### 其他目錄

- `source/SmartBodyAI/SmartBodyAI.Tests`：測試專案
- `docs/`：SMART on FHIR 設計、部署 SOP、測試清單與 FHIR 範例資料
- `DICOM/`：範例 DICOM 檔案

## 技術棧

- ASP.NET Core
- Blazor Server
- SMART on FHIR / HL7 FHIR R4
- OAuth2 / PKCE / OIDC
- fo-dicom
- Ant Design Blazor
- Syncfusion Blazor
- NLog
- xUnit

## 快速開始

### 1. 需求環境

- .NET SDK 10.0
- 可用的 `SMART on FHIR` sandbox 或測試環境
- 可寫入的本機暫存目錄
- Windows 開發環境

### 2. 專案相依性

`SmartAgentApi` 目前包含 repo 外部的專案參考：

- `..\..\..\..\CTMS\src\CTMS\AIAgent.Business\AIAgent.Business.csproj`
- `..\..\..\..\CTMS\src\CTMS\CTMS.Business\CTMS.Business.csproj`

如果本機沒有對應的 `CTMS` 原始碼，`SmartAgentApi` 可能無法正常還原或建置。  
如果只需要閱讀與調整前端 SMART 流程，可先以 `SmartBodyAI` 為主；若要完整驗證 DICOM/AI 流程，需先備妥相關相依專案與執行環境。

### 3. 主要設定

`source/SmartBodyAI/SmartBodyAI/appsettings.json` 內包含以下重要設定：

- `SmartAppSetting:FhirServerUrl`
- `SmartAppSetting:RedirectUrl`
- `SmartAppSetting:InferenceHostApi`
- `SmartAppSetting:ClientId`
- `SmartAppSetting:ClientSecret`
- `SmartAppSetting:AuthorizationScope`
- `SmartAppSetting:UploadDicomTempPath`
- `SmartAppSetting:UploadDicomPath`
- `SmartAppSetting:DicomImagePath`
- `SmartAppSetting:AIResultPath`

目前開發設定中的重要值包含：

- `RedirectUrl`：`https://localhost:7170/patient-information`
- `InferenceHostApi`：`https://localhost:7274`

如果你使用 SMART sandbox 或自建授權環境，請確認 callback URL 與 `RedirectUrl` 完全一致。

### 4. 啟動順序

建議先啟動 API，再啟動主站：

1. 啟動 `SmartAgentApi`
2. 啟動 `SmartBodyAI`
3. 從 SMART launcher 或本機首頁開始授權流程
4. 完成 callback 後驗證病患資料讀取與 DICOM/AI 流程

### 5. 執行方式

你可以分別進入兩個專案目錄後執行：

```powershell
dotnet run
```

常用路徑如下：

- `source/SmartAgentApi/SmartAgentApi`
- `source/SmartBodyAI/SmartBodyAI`

## 設定與安全注意事項

### Client Secret 管理

- 正式環境不要將 `ClientSecret` 寫入版本控制中的設定檔
- 建議透過 `ASP.NET Core User Secrets` 或環境變數提供敏感設定
- 常用環境變數名稱如下：
  - `SmartAppSetting__ClientId`
  - `SmartAppSetting__ClientSecret`

設定 `User Secrets` 的範例：

```powershell
dotnet user-secrets set "SmartAppSetting:ClientId" "<your-client-id>"
dotnet user-secrets set "SmartAppSetting:ClientSecret" "<your-client-secret>"
```

設定環境變數的範例：

```powershell
$env:SmartAppSetting__ClientId="<your-client-id>"
$env:SmartAppSetting__ClientSecret="<your-client-secret>"
```

### 本機路徑

目前主站會使用本機目錄作為 DICOM 暫存、上傳與影像輸出位置。  
預設值位於 `C:\temp\SmartBodyAI\...`，第一次執行時會嘗試自動建立資料夾，請確認執行帳號有足夠權限。

### SMART 啟動與授權

- 首頁會觸發 SMART 啟動流程
- callback 目標頁為 `/patient-information`
- 若使用 sandbox，請同步確認：
  - `client_id`
  - `redirect_uri`
  - `scope`
  - FHIR base URL

## 開發與測試

Repo 內包含 `SmartBodyAI.Tests` 測試專案，可用於驗證 SMART discovery、授權流程與 checklist 相關邏輯。

可於 repo 根目錄或測試專案目錄執行：

```powershell
dotnet test source/SmartBodyAI/SmartBodyAI.Tests/SmartBodyAI.Tests.csproj
```

### 建議手動驗證流程

- SMART 啟動與授權流程可正常完成
- callback 後可成功取得 access token
- 可讀取病患資料與相關 FHIR 資源
- `health-check` 可顯示 SMART readiness 狀態
- `smart-checklist` 可提供人工檢查依據
- DICOM ZIP 可上傳並完成 AI 結果查詢與下載

## 文件參考

完整的文件體系集中於 `docs/`，**請先讀文件總索引** [`docs/README.md`](docs/README.md)，它會依任務類型（新增功能 / 修 bug / 調設定 / 發版）導引該讀哪些文件。

動手前必讀（約束與規範）：

- [`docs/guidelines/constraints.md`](docs/guidelines/constraints.md)
  - 不可違反的硬性約束清單（C-1～C-10）
- [`docs/guidelines/coding-conventions.md`](docs/guidelines/coding-conventions.md)、[`versioning.md`](docs/guidelines/versioning.md)、[`contributing.md`](docs/guidelines/contributing.md)

主要文件：

- [`docs/prd/product-requirements.md`](docs/prd/product-requirements.md)
  - 產品需求文件（定位、功能與非功能需求）
- [`docs/architecture/system-architecture.md`](docs/architecture/system-architecture.md)
  - 系統架構、核心流程；另含 [`adr/`](docs/architecture/adr/) 決策紀錄
- [`docs/architecture/SMART on FHIR App 啟動與授權設計說明.md`](docs/architecture/SMART%20on%20FHIR%20App%20啟動與授權設計說明.md)
  - 補充 SMART discovery、授權流程、callback 與 token exchange 設計背景
- [`docs/reference/`](docs/reference/)
  - 設定參數參考表、API 端點、術語表
- [`docs/operations/`](docs/operations/)
  - 正式環境變數 SOP、測試清單、standalone 檢查表、FHIR 測試資料範例
- [`docs/changelog/CHANGELOG.md`](docs/changelog/CHANGELOG.md)
  - 版本變更紀錄

## 已知限制

- `SmartAgentApi` 依賴外部 `CTMS` 專案，缺少相依原始碼時無法完整建置
- `docs/` 下所有 `.md` 已統一為 UTF-8 含 BOM 並通過 `scripts/Test-DocsEncoding.ps1` 檢驗；若仍發現與程式碼不一致處，請以程式碼與設定檔為主要事實來源
- 目前 README 以開發交接為主，不是對外產品文件
