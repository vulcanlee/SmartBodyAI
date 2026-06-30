# 變更紀錄（Changelog）

本檔記錄 SmartBodyAI 的版本變更。格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，
分類為**新增 / 變更 / 修正 / 回退**。版本號對應 `appsettings.json` 的 `SystemVersion`（見 [`../guidelines/versioning.md`](../guidelines/versioning.md)）。

> 早期 commit 未逐一標註版本號，故 `1.2.36` 之前依**開發里程碑（月份）**整理；自文件體系建立後，後續變更請依版本逐筆登錄。

---

## [Unreleased]

### 新增
- 建立 `docs/` 文件體系（階段 1～3）：
  - 文件總索引 `docs/README.md`
  - 約束與規範 `guidelines/`（constraints、coding-conventions、versioning、contributing）
  - PRD `prd/product-requirements.md`
  - 系統架構 `architecture/system-architecture.md` 與 ADR `architecture/adr/0001~0005`
  - 參考資料 `reference/`（設定參數、API 端點、術語表）
  - 本變更紀錄 `changelog/CHANGELOG.md`
- 將 `CLAUDE.md`、`AGENTS.md`、`scripts/Test-DocsEncoding.ps1` 納入版控。
- 啟動頁（LaunchView）頂部新增成大醫院 (NCKUH) 機構 Logo。
- AI 結果新增「骨骼肌綜合指標 (SMG)」欄位（`BodyAIResult`、`AIResultModel` 與 DICOM 封裝來源 `Total_SMG`）。
- 補齊後端文件落差：新增 `architecture/smartagentapi-inference-pipeline.md`（SmartAgentApi 內部、多階段推論管線、CTMS 相依面、佇列佈局）與 `reference/body-composition-and-risk-models.md`（身體組成指標公式、EC/OC 風險模型、資料 schema）。`system-architecture.md` 新增後端模組職責節（2b）。
- 建立 per-project 產品/規格文件集：新增前後端各自 PRD（`prd/smartbodyai-prd.md`、`prd/smartagentapi-prd.md`，導入 `FE-`/`BE-` 需求編號）、功能規格書 `specs/smartbodyai-srs.md`、`specs/smartagentapi-srs.md`、使用者情境/驗收 `specs/use-cases-and-acceptance.md`（含 E2E）、跨系統資料字典 `reference/data-dictionary.md`，以及各專案 README（`source/SmartBodyAI/README.md`、`source/SmartAgentApi/README.md`）。原 `prd/product-requirements.md` 重構為跨系統總覽並指向兩份 PRD。

### 變更
- 既有 docs 文件補上 UTF-8 BOM、移入分類子目錄（architecture/、operations/）。
- AIResultView 詳細內容表格改為單欄列出 SMA／SMI／SMD／SMG，移除 LAMA／NAMA／IMAT／Myosteatosis 欄位。
- 更新 `operations/smart-on-fhir-standalone-checklist.md`：將「standalone only、不支援 EHR launch」更正為**支援 standalone + EHR launch**，使其與 commit `67c4e02` 後的實作一致。

---

## [1.2.36] - 2026/04/06

### 新增
- 支援 SMART **EHR launch** 與新增九大透明性頁面（`wwwroot/page9.html`）。
- SMART Checklist 支援本機儲存、JSON 匯出／匯入與還原。
- 健康檢查新增系統資訊欄位與對應單元測試。

### 變更
- HealthCheckPage 載入邏輯調整與延遲優化；HealthCheck 載入優化與版本號更新。
- 更新 `FhirServerUrl` 為 `launcher.bdlfhir.net`（開發預設另提供 smarthealthit 公開 sandbox）。

### 回退
- 將 `SystemVersion` 回退至 `1.2.36 (2026/04/06)`。

---

## 里程碑：SMART 服務化、健康檢查與文件（2026/04）

### 新增
- 新增健康檢查頁面與系統診斷服務、SMART readiness 指標。
- 新增 SMART on FHIR Sandbox 檢查清單功能與測試。
- 新增「Blazor SMART on FHIR App 啟動與授權設計說明」與完整 README。
- 新增解決方案檔；強化 logger 記錄請求 URI。

### 變更
- 重構 SMART 授權為服務導向（Service-oriented），強化測試與健康檢查。
- 支援 SMART on FHIR **PKCE** 與授權錯誤處理強化。
- 優化 FHIR 伺服器與 SMART 授權端點自動偵測。
- SMART on FHIR checklist 文件中文化與內容強化。
- 更新本機端 `RedirectUrl` 與 `InferenceHostApi` 設定。

---

## 里程碑：除錯能力與日誌（2026/03）

### 新增
- 支援 OAuth2 `ClientSecret` 並新增安全部署／測試指引。
- 新增 Lottie loading 動畫與 **Debug 模式**控制（`IsDebug`）。
- 新增上傳日誌、錯誤處理與連接 FHIR 伺服器前的日誌紀錄。

### 變更
- 增強日誌與錯誤提示，改善除錯與追蹤能力。

---

## 里程碑：NLog 與 AI 推論串接（2026/02）

### 新增
- 全專案導入 **NLog**，強化前後端日誌記錄。
- 新增 DICOM ZIP 上傳／解壓 API 及 AI 推論資料流程。
- 新增 AI 推論查詢與下載 API，前端自動化整合。
- 新增 AI 分析結果產生與 **CTMS.Business** 專案整合。

### 變更
- 強化 DICOM 上傳與 AI 推論自動化串接；AI 分析結果頁面串接與資料流優化。
- 調整 AI 結果路徑與輪詢通知機制。

### 修正
- 清除完成佇列並修正病人識別碼處理。
- 修正 JSON 檔名拼寫。
- 調整 HttpClient，略過 SSL 憑證驗證（開發測試用途）。
- 移除範例 Controller。

---

## 里程碑：DICOM 上傳與 UI（2026/01）

### 新增
- 專案初始化（Init）。
- 整合 SMART on FHIR OAuth2 授權流程與設定服務、病患資訊頁面。
- 新增 DICOM 上傳與自動轉圖片功能、即時影像顯示。
- 新增 `SmartAgentApi` 專案與 DICOM 暫存路徑支援（`UploadDicomTempPath`）。
- 新增 AI 結果頁；改用病歷號查詢並動態顯示病人與 DICOM 資訊。
- 導入 Syncfusion / Ant Design 與通知元件。

### 變更
- 多次 UI／版面／響應式設計與狀態訊息優化。
- 品牌識別更新（首頁與病人資訊頁）。
- 統一標題與優化流程步驟動態控制；強化啟動與病患資訊流程錯誤處理。

### 修正
- 修正年齡顯示格式並加強 `TokenUrl` 檢查。

---

> 維護提示：依 [`../guidelines/contributing.md`](../guidelines/contributing.md)，**commit 之後**將本次異動補入對應版本區段，並確保版本號與 `appsettings.json` 一致。
