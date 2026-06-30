# 產品需求總覽（PRD Overview）— SmartBodyAI 系統

> 本系統由**前端主站**與**後端 API** 兩個獨立專案組成。本文件為**跨系統總覽**：定位、用戶輪廓、
> 系統級非功能需求、端到端流程與範圍。各專案的詳細產品需求見專屬 PRD：
>
> - 前端：[`smartbodyai-prd.md`](smartbodyai-prd.md)（需求編號 `FE-FR`/`FE-NFR`）
> - 後端：[`smartagentapi-prd.md`](smartagentapi-prd.md)（需求編號 `BE-FR`/`BE-NFR`）
>
> 相關：架構見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)；
> 細部規格見 [`../specs/`](../specs/)；約束見 [`../guidelines/constraints.md`](../guidelines/constraints.md)。

---

## 1. 產品定位

SmartBodyAI 是一個 **SMART on FHIR 病患應用示範系統**，整合 **DICOM 醫學影像**與 **AI 身體組成推論**，用於展示：從 EHR／FHIR Sandbox 啟動並完成 SMART 授權、讀取病患 FHIR 資源、上傳 DICOM、觸發 AI 分析並查詢/下載結果，並提供就緒（readiness）與合規審查工具。

**目前定位為開發與展示用途**，非對外正式產品；文件以開發交接與品質收斂為主。

整體不是單一網站，而是：

- **SmartBodyAI（前端）**：Blazor Server 主站，負責 SMART 啟動/授權、病患資料呈現、DICOM 上傳介面、健康檢查與 checklist。
- **SmartAgentApi（後端）**：REST 閘道，負責接收 DICOM ZIP、入列 CTMS 推論佇列、彙整結果下載。**不驅動**推論管線（由 CTMS 外部執行器驅動）。

---

## 2. 目標用戶

| 用戶 | 需求 |
|------|------|
| 醫療資訊整合開發者 | 驗證 SMART on FHIR 授權與 FHIR 資源讀取流程 |
| AI／影像團隊 | 串接 DICOM 上傳與 AI 推論結果流程 |
| 系統整合／稽核人員 | 透過健康檢查與 SMART checklist 確認部署就緒與合規 |
| 維運人員 | 依 SOP 設定正式環境變數與機密、佇列/模型路徑 |

---

## 3. 功能範圍（依專案分屬）

| 範圍 | 專案 | 詳見 |
|------|------|------|
| SMART 啟動/授權、病患資料、DICOM 上傳介面、health-check、checklist、透明性頁 | 前端 | [`smartbodyai-prd.md`](smartbodyai-prd.md) |
| DICOM 接收/解壓、入列 CTMS、結果狀態查詢、結果下載與指標計算 | 後端 | [`smartagentapi-prd.md`](smartagentapi-prd.md) |

---

## 4. 系統級非功能需求

| 類別 | 需求 |
|------|------|
| **安全** | PKCE S256 強制；機密不進版控；State 防 CSRF；redirect_uri 完全一致；正式環境須恢復 SSL 驗證。見 [`../guidelines/constraints.md`](../guidelines/constraints.md)。 |
| **可觀測性** | 前後端均以 NLog 記錄外部 API Request URI 與關鍵節點；保留 30 天、壓縮歸檔。 |
| **可維運性** | 設定可由環境變數覆寫；前端 Debug 模式可由 `?debug=true` 控制；健康檢查提供完整診斷。 |
| **文件與編碼** | 文件 UTF-8 繁中、docs `.md` 含 BOM、CI 檢驗；異動後同步文件。 |
| **相容性** | .NET 10.0、Windows 開發環境、FHIR R4；後端風險評估需 R（Rscript）環境。 |

---

## 5. 端到端流程

```
SMART launcher / 首頁
  → 啟動(iss/launch/debug) → Discovery(well-known→metadata fallback)
  → 授權(PKCE S256) → callback /patient-information(驗 state、換 token、驗 patient context)
  → 讀取 FHIR 病患資源並呈現
  → 上傳 DICOM ZIP →〔後端入列 CTMS，外部執行器跑推論〕→ 輪詢 → 下載結果
```

詳細時序見 [`../architecture/system-architecture.md`](../architecture/system-architecture.md)；端到端用例與驗收見 [`../specs/use-cases-and-acceptance.md`](../specs/use-cases-and-acceptance.md)。

---

## 6. 不在範圍內（Out of Scope）

- 正式生產級的 OIDC 簽章信任鏈驗證。
- 多租戶／多機構部署管理。
- 病患資料的長期儲存與後端資料庫（目前以即時讀取與本機暫存為主）。
- AI 推論演算法、外部標註/定量分析與 R 風險模型本身（屬 CTMS／外部）。

---

## 7. 已知限制

- `SmartAgentApi` 依賴 repo 外部的 `CTMS`，缺少相依原始碼時無法完整建置（見 [ADR 0005](../architecture/adr/0005-external-ctms-dependency.md)）。
- OIDC 目前為應用層基本驗證，非完整簽章與信任鏈驗證。
- 依賴可寫入的本機目錄。
- 目前以開發交接為主，非對外產品文件。
