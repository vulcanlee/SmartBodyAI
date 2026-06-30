# 系統架構與技術設計 — SmartBodyAI

> 本文件描述整體架構、模組職責與核心流程。SMART 授權的設計細節見同目錄
> [`SMART on FHIR App 啟動與授權設計說明.md`](SMART%20on%20FHIR%20App%20啟動與授權設計說明.md)；
> 既定決策見 [`adr/`](adr/)。

---

## 1. 整體架構

SmartBodyAI 不是單一網站，而是由**前端主站**與**後端 API** 兩個獨立部署的應用組成：

```
┌─────────────────────────┐        ┌──────────────────────────┐
│  SmartBodyAI (前端)      │        │  SmartAgentApi (後端 API)  │
│  Blazor Server           │        │  ASP.NET Core WebAPI       │
│  https://localhost:7170  │        │  https://localhost:7274    │
│                          │        │                            │
│  - SMART 啟動/授權        │  HTTP  │  - DICOM ZIP 接收/解壓      │
│  - 病患 FHIR 資料呈現     │ ─────▶ │  - AI 推論流程調度          │
│  - DICOM 上傳介面         │        │  - 結果儲存/打包/下載       │
│  - 健康檢查/Checklist     │        │                            │
└───────────┬─────────────┘        └─────────────┬─────────────┘
            │                                      │
            ▼                                      ▼
   FHIR 伺服器 / 授權伺服器              外部 CTMS / AI 推論服務
   (SMART on FHIR sandbox)              (AIAgent.Business / CTMS.Business)
```

- **SmartBodyAI**：Blazor Server（Interactive Server Render Mode）主站。
- **SmartAgentApi**：RESTful API，依賴 repo 外部的 `CTMS` 專案（見 [`adr/0005-external-ctms-dependency.md`](adr/0005-external-ctms-dependency.md)）。

---

## 2. 模組職責（SmartBodyAI 前端）

| 目錄 | 職責 | 代表類別 |
|------|------|---------|
| `Components/Pages/` | 完整路由頁面 | `Home`、`LaunchPage`、`PatientInformationPage`、`HealthCheckPage`、`SmartChecklistPage`、`AIResultPage` |
| `Components/Views/` | 可復用視圖元件（含 code-behind） | `LaunchView`、`PatientInformationView`、`AIResultView` |
| `Services/` | 業務邏輯 | `ISmartDiscoveryService`/`SmartDiscoveryService`、`ISmartAuthorizationService`/`SmartAuthorizationService`、`HealthCheckService`、`ConfigurationDiagnosticsService`、`SmartChecklistTemplateService`、`SmartChecklistPersistenceService` |
| `Servicers/` | 基礎設施服務 | `OAuthStateStoreService`、`DicomService`、`SettingService`、`SmartAppSettingService` |
| `Models/` | 資料模型與 DTO | `SettingModel`、`SmartAppSettingModel`、`HealthCheckSummary`、`HealthIndicatorResult`、`PatientInformationModel`、`SmartChecklist*` |
| `Helpers/` | 靜態工具 | `PkceHelper`、`MagicObjectHelper` |
| `wwwroot/` | 靜態資產 | `page9.html`（九大透明性頁） |

### DI 服務註冊（`Program.cs`）
```
SettingService、SmartAppSettingService、OAuthStateStoreService、
DicomService、ConfigurationDiagnosticsService、HealthCheckService、
SmartChecklistTemplateService、SmartChecklistPersistenceService、
ISmartDiscoveryService→SmartDiscoveryService、
ISmartAuthorizationService→SmartAuthorizationService
```

---

## 2b. 模組職責（SmartAgentApi 後端）

後端是輕量 REST 閘道：接收 DICOM ZIP、入列 CTMS 佇列、彙整結果下載。**本身不驅動推論管線**（管線由 CTMS 外部執行器跑）。

| 目錄 | 職責 | 代表類別 |
|------|------|---------|
| `Controllers/` | 對外端點與入列 | `DicomPackController`（`Upload`/`CheckResult`/`Download`/`PushToAICheck`） |
| `Models/` | 結果與病患 DTO | `BodyAIResult`、`PatientDataModel`（後者目前僅 `PushToAiService` 用） |
| `Services/` | 入列封裝 | `PushToAiService`（目前未註冊、未使用） |
| `Program.cs` | 主機/DI | 註冊 CTMS 服務（`AgentService`、`AIIntegrateService` 等） |

> 後端的多階段推論管線（InBound→Phase1/2/3→Complete）、CTMS 相依面、佇列佈局與時序，詳見
> [`smartagentapi-inference-pipeline.md`](smartagentapi-inference-pipeline.md)；身體組成指標公式、EC/OC 風險模型與資料 schema 見
> [`../reference/body-composition-and-risk-models.md`](../reference/body-composition-and-risk-models.md)。

---

## 3. 核心業務流程

### 3.1 啟動 → 授權 → 病患資料
```
1. 首頁 (/) 或 /launch?iss=...&launch=...&debug=...
2. LaunchView 註冊 iss/launch/debug 參數
3. SmartDiscoveryService.DiscoverAsync(fhirServerUrl)
   - 先 {fhir}/.well-known/smart-configuration，失敗 fallback {fhir}/metadata
   - 驗證 authorization_endpoint / token_endpoint / capabilities
4. SmartAuthorizationService.CreateAuthorizationRequestAsync(...)
   - 產生 PKCE code_verifier / code_challenge（S256）
   - 產生唯一 state，存入 OAuthStateStoreService（TTL 10 分鐘）
   - 組授權 URL（含 aud、launch、code_challenge），導向授權伺服器
5. 授權伺服器驗證並導回 /patient-information?code=...&state=...
6. PatientInformationView：
   - 驗證 state（不符即失敗並清除）
   - 以 code + code_verifier 交換 token（ExchangeCodeForTokenAsync）
   - 驗證 token_type=Bearer、access_token、patient context
   - 解析 id_token，驗證 fhirUser/sub/iss
7. 以 Bearer token 讀取 FHIR 資源（Patient、Observation 等）並呈現
```

### 3.2 DICOM 上傳 → AI 結果
```
8.  病患資訊頁上傳 DICOM ZIP → POST /DicomPack（SmartAgentApi）
9.  後端解壓、推送 AI 流程、回傳工作識別碼（checkKey）
10. 輪詢 GET /DicomPack/CheckResult/{checkKey}
11. 完成後 GET /DicomPack/Download/{checkKey} 下載結果 ZIP
```
端點規格見 [`../reference/api-reference.md`](../reference/api-reference.md)；後端入列後的多階段推論管線（由 CTMS 外部執行器驅動）與 CTMS 相依面，見 [`smartagentapi-inference-pipeline.md`](smartagentapi-inference-pipeline.md)。

---

## 4. 健康檢查（HealthCheckService）

`/health-check` 蒐集系統資訊、設定值與運行環境，產生多項 `HealthIndicatorResult`，涵蓋：

- 設定繫結與必要欄位、ClientCredential、環境變數覆寫、ISS 參數覆寫、RedirectUrl
- 目錄權限（上傳／暫存／影像／結果路徑）、樣本資料、DICOM 靜態路徑
- FHIR 連線、SMART configuration、metadata fallback、OAuth endpoint
- Standalone capability、OIDC capability、推論服務、授權先決條件、Basic Auth、Launch query 一致性

結果以綠／黃／紅燈與加權分數彙整為 `HealthCheckSummary`。

---

## 5. 技術棧

- **框架**：ASP.NET Core 10.0、Blazor Server
- **醫療標準**：SMART on FHIR、HL7 FHIR R4（`Hl7.Fhir.R4`）、DICOM（`fo-dicom`）
- **授權**：OAuth2 / OIDC / PKCE（S256）、`System.IdentityModel.Tokens.Jwt`
- **UI**：Ant Design Blazor、Syncfusion Blazor
- **日誌**：NLog（`nlog.config`）
- **測試**：xUnit（`SmartBodyAI.Tests`）

---

## 6. 重要設定與事實來源

- 前端設定：`source/SmartBodyAI/SmartBodyAI/appsettings.json`（`SmartAppSetting`）
- 後端設定：`source/SmartAgentApi/SmartAgentApi/appsettings.json`（`AgentSetting`）
- 設定鍵完整說明見 [`../reference/configuration-reference.md`](../reference/configuration-reference.md)。

---

## 7. 關鍵原始碼位置

| 功能 | 路徑 |
|------|------|
| SMART Discovery | `source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs` |
| SMART 授權 | `source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs` |
| OAuth State 儲存 | `source/SmartBodyAI/SmartBodyAI/Servicers/OAuthStateStoreService.cs` |
| 健康檢查 | `source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs` |
| PKCE | `source/SmartBodyAI/SmartBodyAI/Helpers/PkceHelper.cs` |
| DICOM API 控制器 | `source/SmartAgentApi/SmartAgentApi/Controllers/DicomPackController.cs` |
