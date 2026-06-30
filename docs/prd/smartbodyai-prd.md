# 產品需求文件（PRD）— SmartBodyAI（前端主站）

> 本文件描述前端 `SmartBodyAI`（Blazor Server）的產品定位、用戶、功能與非功能需求。
> 跨系統總覽見 [`product-requirements.md`](product-requirements.md)；後端見 [`smartagentapi-prd.md`](smartagentapi-prd.md)。
> 細部規格見 [`../specs/smartbodyai-srs.md`](../specs/smartbodyai-srs.md)；架構見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)。
>
> 需求編號規則：功能 `FE-FR-xx`、非功能 `FE-NFR-xx`，供 SRS 與驗收文件回溯引用。

---

## 1. 產品定位

`SmartBodyAI` 是一個 **SMART on FHIR 病患應用（Patient App）前端**，負責：從 EHR／FHIR Sandbox 啟動並完成 SMART 授權、讀取病患 FHIR 資源、提供 DICOM 上傳與 AI 身體組成分析的操作介面，並提供部署就緒（readiness）與合規審查工具。

定位為**開發與展示用途**，非對外正式產品。前端不執行 AI 推論，AI 與 DICOM 處理委派後端 [`SmartAgentApi`](smartagentapi-prd.md)。

---

## 2. 目標用戶

| 用戶 | 需求 |
|------|------|
| 醫療資訊整合開發者 | 驗證 SMART on FHIR 授權與 FHIR 讀取流程 |
| 臨床/影像操作者（展示情境） | 上傳 DICOM、查看 AI 身體組成結果 |
| 系統整合／稽核人員 | 以 health-check 與 smart-checklist 確認就緒與合規 |

---

## 3. 功能需求

### 3.1 SMART 啟動與授權
- **FE-FR-01 啟動入口**：支援 Standalone Launch 與 EHR Launch，接收查詢參數 `iss`、`launch`、`debug`（路由 `/`、`/launch`）。
- **FE-FR-02 SMART Discovery**：優先 `/.well-known/smart-configuration`，失敗時 fallback `/metadata`（CapabilityStatement），並驗證授權/Token 端點與必要能力。
- **FE-FR-03 授權請求**：OAuth2 Authorization Code Flow，強制 **PKCE S256**（見 [C-5](../guidelines/constraints.md)）。
- **FE-FR-04 State 管理**：生成唯一 `state`、保存、callback 嚴格比對、流程結束清理；TTL **10 分鐘**（見 [C-6](../guidelines/constraints.md)）。
- **FE-FR-05 Token 交換與驗證**：以 code + code_verifier 交換 token；驗證 `token_type=Bearer`、`access_token`、patient context，並解析 `id_token`（fhirUser/sub/iss）（見 [C-8](../guidelines/constraints.md)）。callback 目標固定 `/patient-information`（[C-7](../guidelines/constraints.md)）。

### 3.2 病患資料
- **FE-FR-06 FHIR 資源讀取**：以 Bearer Token 讀取 Patient、Observation（vital-signs 身高/體重）、Encounter、MedicationRequest、ServiceRequest，於病患資訊頁呈現。

### 3.3 DICOM 與 AI
- **FE-FR-07 DICOM 上傳介面**：提供上傳對話框（`UploadDicomDialog`），收集病患資料並打包 `PatientData.json` + DICOM。
- **FE-FR-08 AI 推論協調**：上傳至後端 `InferenceHostApi`、輪詢結果狀態、下載結果 ZIP 並於 AI 結果頁（`/AIResult/{RandomCode}`）呈現身體組成指標與影像。
- **FE-FR-12 流程進度**：以 5 步驟（確認基本資料→上傳 DICOM→進行 AI 推論→查看 AI 分析→查看結果）呈現整體進度。

### 3.4 就緒與合規工具
- **FE-FR-09 健康檢查（Readiness）**：`/health-check` 產生系統診斷報告（設定繫結、必要欄位、機密、環境變數覆寫、目錄權限、FHIR 連線、SMART configuration、OAuth endpoint、Standalone/OIDC capability、推論服務等），以綠/黃/紅燈與加權分數呈現。
- **FE-FR-10 SMART Checklist**：`/smart-checklist` 提供人工審查清單（自動計分、三燈狀態、LocalStorage 儲存、JSON 匯出/匯入還原）。
- **FE-FR-11 透明性頁**：提供九大透明性靜態頁（`wwwroot/page9.html`）。

---

## 4. 非功能需求

| 編號 | 類別 | 需求 |
|------|------|------|
| FE-NFR-01 | 安全 | PKCE S256 強制；`ClientSecret` 不進版控（環境變數／User Secrets）；State 防 CSRF；`redirect_uri` 完全一致。見 [`constraints.md`](../guidelines/constraints.md) C-1/C-5/C-6/C-7。 |
| FE-NFR-02 | 可觀測性 | NLog 記錄外部 API（FHIR/授權/推論）Request URI 與流程節點，不記錄機密（[C-9](../guidelines/constraints.md)）。 |
| FE-NFR-03 | 可維運性 | 設定可由環境變數覆寫；`?debug=true` 啟用偵錯；health-check 提供完整診斷。 |
| FE-NFR-04 | 相容性 | .NET 10.0、Blazor Server（Interactive Server）、HL7 FHIR R4、Windows 開發環境。 |
| FE-NFR-05 | 文件與編碼 | 文件 UTF-8 繁中、docs `.md` 含 BOM、異動後同步（[C-3](../guidelines/constraints.md)/[C-4](../guidelines/constraints.md)）。 |

---

## 5. 主要使用情境（概要）

```
SMART launcher / 首頁(/)
  → 啟動(iss/launch/debug) → Discovery(well-known→metadata fallback)
  → 授權(PKCE S256) → callback /patient-information(驗 state、換 token、驗 patient context)
  → 讀取 FHIR 病患資源並呈現
  → 上傳 DICOM ZIP → 輪詢 AI 狀態 → 下載並呈現結果
```

詳細用例與驗收見 [`../specs/use-cases-and-acceptance.md`](../specs/use-cases-and-acceptance.md)。

---

## 6. 範圍與限制

- **不在範圍**：AI 推論計算本身（由後端與 CTMS 處理）、正式生產級 OIDC 簽章信任鏈驗證、多租戶部署、病患資料長期儲存。
- **限制**：OIDC 為應用層基本驗證；依賴可寫入的本機暫存目錄；以開發/展示為主。
