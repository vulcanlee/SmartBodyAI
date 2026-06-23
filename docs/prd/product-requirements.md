# 產品需求文件（PRD）— SmartBodyAI

> 本文件描述 SmartBodyAI 的產品定位、目標用戶、功能與非功能需求。新增需求時，請確認與本文件定位一致；若需擴張範圍，先更新本文件再實作。
>
> 相關文件：架構見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)；約束見 [`../guidelines/constraints.md`](../guidelines/constraints.md)。

---

## 1. 產品定位

SmartBodyAI 是一個 **SMART on FHIR 病患應用（Patient App）示範專案**，整合 **DICOM 醫學影像** 與 **AI 推論**，用於展示：

- 從 EHR／FHIR Sandbox 啟動並完成 SMART on FHIR 授權
- 讀取病患 FHIR 資源並呈現身體組成相關資訊
- 上傳 DICOM 影像、觸發 AI 分析、查詢與下載結果
- 提供完整的健康檢查（readiness）與合規檢查工具

**目前定位為開發與展示用途**，非對外的正式產品；文件以開發交接與品質收斂為主。

---

## 2. 目標用戶

| 用戶 | 需求 |
|------|------|
| 醫療資訊整合開發者 | 驗證 SMART on FHIR 授權與 FHIR 資源讀取流程 |
| AI／影像團隊 | 串接 DICOM 上傳與 AI 推論結果流程 |
| 系統整合／稽核人員 | 透過健康檢查與 SMART checklist 確認部署就緒與合規 |
| 維運人員 | 依 SOP 設定正式環境變數與機密 |

---

## 3. 功能需求

### 3.1 SMART on FHIR 啟動與授權
- 支援 **Standalone Launch** 與 **EHR Launch**（接收 `iss`、`launch` 參數）。
- SMART Discovery：優先讀取 `/.well-known/smart-configuration`，失敗時 fallback 至 `/metadata`（CapabilityStatement）。
- OAuth2 Authorization Code Flow + **PKCE（S256）**。
- 支援 Confidential Client（可選 `ClientSecret`）與 OIDC（`openid fhirUser profile`）。
- State 產生／驗證／清理，10 分鐘 TTL，防 CSRF。

### 3.2 病患資料讀取
- 以 Bearer Token 呼叫 FHIR API，讀取 Patient、Observation（身高／體重）、Encounter、MedicationRequest、ServiceRequest 等資源。
- 解析 `id_token`，驗證 `fhirUser`、`sub`、`iss` claim。
- 取得並驗證 patient context。

### 3.3 DICOM 與 AI 整合
- 於病患資訊頁上傳 DICOM ZIP 至後端 `SmartAgentApi`。
- 後端解壓、推送 AI 推論流程、回傳工作識別碼。
- 輪詢查詢 AI 結果狀態，完成後下載結果 ZIP。

### 3.4 健康檢查（Readiness）
- `/health-check` 頁面產生系統診斷報告，涵蓋設定繫結、必要欄位、機密、環境變數覆寫、目錄權限、FHIR 連線、SMART configuration、OAuth endpoint、Standalone / OIDC capability、推論服務等多項指標。
- 以綠／黃／紅燈與加權分數呈現就緒程度。

### 3.5 SMART Checklist（人工審查工具）
- `/smart-checklist` 提供人工檢查清單，含自動計分、綠／黃／紅燈狀態。
- 支援本機儲存（LocalStorage）、JSON 匯出／匯入與還原。

### 3.6 透明性頁面
- 提供九大透明性揭露靜態頁（`wwwroot/page9.html`），說明應用透明性內容。

---

## 4. 非功能需求

| 類別 | 需求 |
|------|------|
| **安全** | PKCE S256 強制；機密不進版控（環境變數／User Secrets）；State 防 CSRF；redirect_uri 完全一致。見 [`../guidelines/constraints.md`](../guidelines/constraints.md)。 |
| **可觀測性** | NLog 記錄外部 API Request URI 與授權流程節點；保留 30 天、壓縮歸檔。 |
| **可維運性** | 設定可由環境變數覆寫；Debug 模式可由查詢參數 `?debug=true` 控制；健康檢查提供完整診斷。 |
| **文件與編碼** | 文件 UTF-8 繁中、docs `.md` 含 BOM、CI 檢驗；異動後同步文件。 |
| **相容性** | .NET 10.0、Windows 開發環境、FHIR R4。 |

---

## 5. 主要使用情境（流程概要）

```
SMART launcher / 首頁
  → 啟動（iss/launch/debug）
  → Discovery（well-known → metadata fallback）
  → 授權（PKCE S256，導向授權伺服器）
  → callback /patient-information（驗 state、換 token、驗 patient context）
  → 讀取 FHIR 病患資源並呈現
  → 上傳 DICOM ZIP → AI 推論 → 查詢 → 下載結果
```

詳細流程與時序見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)。

---

## 6. 已知限制

- `SmartAgentApi` 依賴 repo 外部的 `CTMS` 專案（`AIAgent.Business`、`CTMS.Business`），缺少相依原始碼時無法完整建置（見 [`../architecture/adr/0005-external-ctms-dependency.md`](../architecture/adr/0005-external-ctms-dependency.md)）。
- OIDC 目前為應用層基本驗證，非完整簽章與信任鏈驗證。
- 依賴可寫入的本機目錄（`C:\temp\SmartBodyAI\...`）。
- 目前以開發交接為主，非對外產品文件。

---

## 7. 不在範圍內（Out of Scope）

- 正式生產級的 OIDC 簽章信任鏈驗證。
- 多租戶／多機構部署管理。
- 病患資料的長期儲存與後端資料庫（目前以即時讀取與本機暫存為主）。
