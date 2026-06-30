# 功能規格書（SRS）— SmartBodyAI（前端）

> 本文件為前端 `SmartBodyAI` 的細部功能規格，逐功能描述輸入/前置/行為/輸出/錯誤處理，
> 對應 [`../prd/smartbodyai-prd.md`](../prd/smartbodyai-prd.md) 的 `FE-FR` 需求編號。
> 架構與流程見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)；
> 資料模型見 [`../reference/data-dictionary.md`](../reference/data-dictionary.md)。
>
> 事實來源：`source/SmartBodyAI/SmartBodyAI/{Services,Servicers,Components,Models}`。

---

## S-1（FE-FR-01/02）啟動與 Discovery

- **入口**：頁面 `/`（`Home`，參數 `Iss`、`IsDebug`）、`/launch`（`LaunchPage`，參數 `ISS`、`Launch`、`IsDebug`、`Code`、`State`），均委派 `LaunchView`。
- **行為**（`LaunchView.OnAfterRenderAsync`）：
  1. 以 `Iss`、`IsDebug` 更新 `SmartAppSettingService.Data`。
  2. `ISmartDiscoveryService.DiscoverAsync(fhirServerUrl)`：先取 `/.well-known/smart-configuration`，失敗 fallback `/metadata`，回傳 `SmartDiscoveryResult`（含 `AuthorizeUrl`、`TokenUrl`、`Capabilities`、`MetadataSource`、`ValidationErrors`、`IsValid`）。
  3. Discovery 成功 → 進入 S-2。
- **錯誤處理**：Discovery 失敗或 `IsValid=false` → 以 AntDesign Notification 顯示錯誤，不導向授權。

## S-2（FE-FR-03/04）授權請求與 State

- **方法**：`ISmartAuthorizationService.CreateAuthorizationRequestAsync(authorizeUrl, SmartAuthorizationRequestContext)` → `SmartAuthorizationRequest`。
- **行為**：以 `PkceHelper` 產生 `code_verifier` 與 `code_challenge`（S256）；產生唯一 `state` 存入 `OAuthStateStoreService`（鍵 `oauth:state:{stateId}`，TTL 10 分鐘）；組授權 URL（含 `aud`、`launch`、`code_challenge`、`redirect_uri`）並導向授權伺服器。
- **約束**：`code_challenge_method` 固定 `S256`（[C-5](../guidelines/constraints.md)）；`redirect_uri` 須與設定一致（[C-7](../guidelines/constraints.md)）。

## S-3（FE-FR-05）Callback 與 Token 交換

- **入口**：`/patient-information`（`PatientInformationView`，參數 `Code`、`State`、`Error`、`ErrorDescription`）。
- **行為**：
  1. `ValidateCallbackAsync(code, state, error, errorDescription)` → 由 store 比對 `state`，不符或有 `error` 即失敗並清理。
  2. `ExchangeCodeForTokenAsync(tokenUrl, code, state, clientId, clientSecret?, redirectUri)`：POST form-urlencoded（帶 `code_verifier`；有 `clientSecret` 時用 Basic Auth）。
  3. 驗證 `token_type=Bearer`、`access_token` 存在、patient context 存在；解析 `id_token`（`fhirUser`/`sub`/`iss`）。回傳 `SmartTokenValidationResult`。
- **輸出**：建立 `SmartResponse`（`AccessToken`/`PatientId`/`Scopes`/`IdToken`/`FhirUser`/`TokenType`）。
- **錯誤處理**：`tokenResult.IsValid=false` → 顯示訊息、延遲後返回首頁；state 無效 → 不繼續。

## S-4（FE-FR-06）病患 FHIR 資料

- **行為**：以 `Hl7.Fhir` `FhirClient`（Bearer header）讀取 Patient 與相關資源，填入 `PatientInformationModel`（`Id`/`Name`/`BirthDate`/`Gender`/`HeightValue`/`WeightValue` 等，含 `GetAge*`/`GetHeight*` 等衍生方法）。
- **資源**：Patient、Observation（vital-signs 身高/體重）、Encounter、MedicationRequest、ServiceRequest（清單見 [`system-architecture.md` 3.1](../architecture/system-architecture.md)）。
- **錯誤處理**：讀取失敗記錄日誌並提示；Patient 不可為 null（[C-8](../guidelines/constraints.md)）。

## S-5（FE-FR-07/08/12）DICOM 上傳與 AI 協調

- **介面**：`UploadDicomDialog` 收集病患資料；以 `PatientDataModel`（`Age`/`Code`/`Gender`/`Height`/`Weight`）組 `PatientData.json`，連同 DICOM 打包上傳。
- **行為**（`PatientInformationView` AI 流程）：POST `{InferenceHostApi}/DicomPack` → 取得 `WorkId` → 輪詢 `GET /DicomPack/CheckResult/{checkKey}` → 完成後 `GET /DicomPack/Download/{checkKey}` 下載並解壓 → 於 `/AIResult/{RandomCode}` 呈現。
- **進度**：`ProcessModel` 以 `ProcessStepEnum`（確認基本資料/上傳 DICOM/進行 AI 推論/查看 AI 分析/查看結果）標記步驟；支援 `CancellationTokenSource` 中止；HTTP 逾時較長以容忍推論等待。
- **錯誤處理**：上傳/輪詢/下載失敗記錄日誌並提示；可中止。

## S-6（FE-FR-09）健康檢查

- **方法**：`HealthCheckService.GenerateAsync(queryIss, queryLaunch, queryDebug)` → `HealthCheckSummary`。
- **行為**：彙整系統資訊、設定來源診斷（`ConfigurationDiagnosticsService`，追蹤 appsettings/環境變數/query 來源）、多項 `HealthIndicatorResult`（綠/黃/紅，分數 Green=100、Yellow=50、Red=0），輸出 `TotalScore`、`Green/Yellow/RedCount`、`Indicators`、`ConfigurationFields`、`Highlights` 及 `BoundSettingJson`/`RuntimeSettingJson`。
- **頁面**：`/health-check`（參數 `Iss`/`Launch`/`Debug` 可覆寫）。

## S-7（FE-FR-10）SMART Checklist

- **建模**：`SmartChecklistTemplateService.CreatePageModel()` → `SmartChecklistPageModel`（8 分類、30+ 項；每項 `SmartChecklistItem`：`Key`/`Title`/`IsRequired`/`Weight`/`Status`/`WhyItMatters`/`Rule`/`TestResult`/`FailureReason`/`ImprovementSuggestion`）。
- **計分**：項目分數 Green=100、Yellow=60、Red=0；必要項 weight=2、選擇項 weight=1，加權平均；判讀：任何必要項 Red 或總分 <60 → 「難以通過」；必要項 Yellow → 「有風險」；總分 ≥85 且無必要 Yellow → 「可望通過」。
- **持久化**：`SmartChecklistPersistenceService`（`CreateSnapshot`/`ApplySnapshot`/`Serialize`/`Deserialize`，`CurrentSnapshotVersion=1`）；前端支援 LocalStorage 儲存與 JSON 匯出/匯入。
- **頁面**：`/smart-checklist`。

## S-8（FE-FR-11）透明性頁

- 靜態頁 `wwwroot/page9.html`，呈現九大透明性揭露內容。

---

## 共用基礎設施

- **DI（`Program.cs`，Scoped）**：`SettingService`、`SmartAppSettingService`、`OAuthStateStoreService`、`DicomService`、`ConfigurationDiagnosticsService`、`HealthCheckService`、`SmartChecklistTemplateService`、`SmartChecklistPersistenceService`、`ISmartDiscoveryService→SmartDiscoveryService`、`ISmartAuthorizationService→SmartAuthorizationService`。
- **DICOM 轉圖**：`DicomService.ConvertSingleFile(dicomPath, pngPath)`（fo-dicom + ImageSharp）；靜態檔以 `PhysicalFileProvider` 對映 `/DicomImages`。
- **日誌**：NLog 記錄外部 API Request URI（[C-9](../guidelines/constraints.md)）。
- **錯誤處理**：非開發環境 `UseExceptionHandler("/Error")` + HSTS；404 重導 `/not-found`。
