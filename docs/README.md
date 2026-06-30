# SmartBodyAI 文件總索引

本目錄收錄 SmartBodyAI 專案的所有說明文件。**新增需求或修正錯誤前，請先閱讀本索引**，依任務類型找到對應文件，並遵守其中的約束條件，避免做出違反規範的變更。

> 本專案另有根目錄的三份入口文件：`README.md`（專案總覽與快速開始）、`CLAUDE.md` / `AGENTS.md`（AI 與開發者的行為準則）。它們是首要入口，會指向本目錄。

---

## 一、依任務類型，該先讀哪些文件

| 你的任務 | 建議閱讀順序 |
|---------|------------|
| **新增功能 / 修改既有功能** | `guidelines/constraints.md` → `prd/product-requirements.md`（及各專案 PRD）→ `architecture/system-architecture.md` → 相關 `reference/` |
| **撰寫 / 檢視需求規格** | `prd/`（PRD）→ `specs/`（SRS、使用者情境/驗收）→ `reference/data-dictionary.md` |
| **修正錯誤（Bug Fix）** | `guidelines/constraints.md` → `architecture/system-architecture.md` → `reference/`（設定 / API）→ `architecture/adr/`（理解既定決策，避免誤改） |
| **調整設定 / 部署** | `reference/configuration-reference.md` → `operations/production-environment-variable-sop.md` |
| **串接 SmartAgentApi** | `reference/api-reference.md` → `architecture/system-architecture.md` |
| **理解後端 AI 推論流程 / CTMS 相依** | `architecture/smartagentapi-inference-pipeline.md` → `reference/body-composition-and-risk-models.md` → `architecture/adr/0005-external-ctms-dependency.md` |
| **理解 SMART on FHIR 授權流程** | `architecture/SMART on FHIR App 啟動與授權設計說明.md` → `architecture/adr/` |
| **發版 / 提交變更** | `guidelines/versioning.md` → `guidelines/contributing.md` → `changelog/CHANGELOG.md` |
| **不熟悉領域術語** | `reference/glossary.md` |

---

## 二、文件分類

### guidelines/ — 約束與規範（**最高優先，所有人必讀**）
- [`constraints.md`](guidelines/constraints.md) — 不可違反的硬性約束清單（含原因與對應程式碼）。
- [`coding-conventions.md`](guidelines/coding-conventions.md) — 命名、目錄、編碼慣例。
- [`versioning.md`](guidelines/versioning.md) — 版本號規則與遞增方式。
- [`contributing.md`](guidelines/contributing.md) — 建置、測試、提交、文件同步流程。

### prd/ — 產品需求
- [`product-requirements.md`](prd/product-requirements.md) — 跨系統產品總覽（定位、用戶、系統級非功能、端到端流程）。
- [`smartbodyai-prd.md`](prd/smartbodyai-prd.md) — 前端 SmartBodyAI PRD（需求編號 `FE-FR`/`FE-NFR`）。
- [`smartagentapi-prd.md`](prd/smartagentapi-prd.md) — 後端 SmartAgentApi PRD（需求編號 `BE-FR`/`BE-NFR`）。

### specs/ — 規格與驗收
- [`smartbodyai-srs.md`](specs/smartbodyai-srs.md) — 前端功能規格書（逐功能 I/O、行為、錯誤處理）。
- [`smartagentapi-srs.md`](specs/smartagentapi-srs.md) — 後端功能規格書（端點契約、內部行為、錯誤碼）。
- [`use-cases-and-acceptance.md`](specs/use-cases-and-acceptance.md) — 使用者情境與 Given-When-Then 驗收（含跨系統 E2E）。

### architecture/ — 架構與技術設計
- [`system-architecture.md`](architecture/system-architecture.md) — 系統架構、模組職責、核心流程。
- [`smartagentapi-inference-pipeline.md`](architecture/smartagentapi-inference-pipeline.md) — 後端 SmartAgentApi 內部、多階段推論管線與 CTMS 相依面（工程深入）。
- [`SMART on FHIR App 啟動與授權設計說明.md`](architecture/SMART%20on%20FHIR%20App%20啟動與授權設計說明.md) — SMART 授權設計細節。
- [`adr/`](architecture/adr/) — 架構決策紀錄（ADR）。

### reference/ — 參考資料
- [`configuration-reference.md`](reference/configuration-reference.md) — appsettings 設定參數參考表。
- [`api-reference.md`](reference/api-reference.md) — SmartAgentApi 端點規格。
- [`body-composition-and-risk-models.md`](reference/body-composition-and-risk-models.md) — 身體組成指標公式、EC/OC 風險模型與資料 schema。
- [`data-dictionary.md`](reference/data-dictionary.md) — 跨系統資料模型與介面契約。
- [`glossary.md`](reference/glossary.md) — 術語表。

### operations/ — 運維與測試
- [`production-environment-variable-sop.md`](operations/production-environment-variable-sop.md) — 正式環境變數設定 SOP。
- [`second-stage-testing-checklist.md`](operations/second-stage-testing-checklist.md) — 第二階段測試準備清單。
- [`smart-on-fhir-standalone-checklist.md`](operations/smart-on-fhir-standalone-checklist.md) — Standalone 規格檢查表。
- [`fhir-sample-bundle.json`](operations/fhir-sample-bundle.json) — FHIR 測試資料範例。

### changelog/ — 異動紀錄
- [`CHANGELOG.md`](changelog/CHANGELOG.md) — 版本變更紀錄（Keep a Changelog 格式）。

---

## 三、文件維護規則

1. **編碼**：本目錄下所有 `.md` 必須是 **UTF-8 含 BOM**，不得有亂碼。提交前以 `pwsh scripts/Test-DocsEncoding.ps1` 驗證。
2. **同步**：程式碼異動後，須同步更新受影響的文件（特別是 `reference/`、`architecture/`、`CHANGELOG.md`）。
3. **事實來源**：當文件與程式碼矛盾時，以程式碼與設定檔為準，並回報修正文件。
4. 詳細規則見 [`guidelines/constraints.md`](guidelines/constraints.md)。
