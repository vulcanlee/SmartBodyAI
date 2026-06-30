# 產品需求文件（PRD）— SmartAgentApi（後端 API）

> 本文件描述後端 `SmartAgentApi`（ASP.NET Core WebAPI）的產品定位、用戶、功能與非功能需求。
> 跨系統總覽見 [`product-requirements.md`](product-requirements.md)；前端見 [`smartbodyai-prd.md`](smartbodyai-prd.md)。
> 細部規格見 [`../specs/smartagentapi-srs.md`](../specs/smartagentapi-srs.md)；內部管線見
> [`../architecture/smartagentapi-inference-pipeline.md`](../architecture/smartagentapi-inference-pipeline.md)。
>
> 需求編號規則：功能 `BE-FR-xx`、非功能 `BE-NFR-xx`。

---

## 1. 產品定位

`SmartAgentApi` 是一個**輕量 REST 閘道**：接收前端上傳的 DICOM 壓縮包、將個案放進 CTMS 推論佇列、並在結果完成後彙整指標供下載。

**重要邊界**：本服務**不執行也不驅動**多階段推論管線——它只把個案入列 `InBound`；管線（`AgentService.RunAsync`）由 **CTMS 端的外部執行器**驅動（細節見 [inference-pipeline](../architecture/smartagentapi-inference-pipeline.md)）。

---

## 2. 目標用戶

| 用戶 | 需求 |
|------|------|
| 前端 `SmartBodyAI` | 上傳 DICOM、輪詢狀態、下載結果的 HTTP 介面 |
| AI／影像團隊 | 將病患資料與 DICOM 接入 CTMS 推論佇列 |
| 維運人員 | 設定佇列/模型路徑、監看日誌、確認外部相依就緒 |

---

## 3. 功能需求

- **BE-FR-01 接收 DICOM ZIP**：`POST /DicomPack`（multipart/form-data，欄位 `file`），僅接受 `.zip`，大小上限約 1 GB。
- **BE-FR-02 解壓與隔離**：以 `workId`（時間戳_Guid）建立獨立工作目錄解壓，避免併發衝突。
- **BE-FR-03 入列 CTMS**：讀取 `PatientData.json`、複製 DICOM、呼叫 `AgentService.CreateInBound` 入列 `InBound`。本 API 將 `癌別` 固定為 `EC`。
- **BE-FR-04 查詢結果狀態**：`GET /DicomPack/CheckResult/{checkKey}`，依完成佇列目錄判斷是否就緒。
- **BE-FR-05 結果下載與指標計算**：`GET /DicomPack/Download/{checkKey}`，讀取 `input.csv` 與 `PatientData.json`，計算身體組成指標（SMA/SMI/SMD/SMG/IMAT/LAMA/NAMA/Myosteatosis）寫入 `BodyAIResult.json`，將完成目錄壓成 ZIP 回傳。指標公式見 [body-composition](../reference/body-composition-and-risk-models.md)。
- **BE-FR-06 日誌**：以 NLog 記錄上傳、解壓、入列、查詢與下載的關鍵節點。

---

## 4. 非功能需求

| 編號 | 類別 | 需求 |
|------|------|------|
| BE-NFR-01 | 外部相依 | 參考 repo 外部 `CTMS`（`AIAgent.Business`、`CTMS.Business`）；缺相依原始碼無法建置（[C-10](../guidelines/constraints.md)、[ADR 0005](../architecture/adr/0005-external-ctms-dependency.md)）。 |
| BE-NFR-02 | 邊界 | 僅負責入列與彙整，不驅動推論管線；個案推進取決於 CTMS 外部執行器與外部標註/定量分析/R 環境。 |
| BE-NFR-03 | 檔案系統 | 依賴本機佇列目錄（`QueueFolderPath` 等）與模型路徑；須具備讀寫權限。 |
| BE-NFR-04 | 可觀測性 | NLog 檔案/控制台輸出，30 天保留、壓縮歸檔。 |
| BE-NFR-05 | 安全 | 開發環境曾略過 SSL 憑證驗證（見 CHANGELOG 2026/02），正式環境須恢復；目前未啟用驗證/CORS。 |
| BE-NFR-06 | 相容性 | .NET 10.0、Windows 環境、需 R 執行環境（Rscript）供風險評估。 |

---

## 5. 主要使用情境（概要）

```
前端 ─POST /DicomPack(zip)→ 解壓 → CreateInBound(InBound)   （入列即返回，推論未啟動）
CTMS 執行器(外部) 反覆 RunAsync：InBound→Phase1/2/3→OutBound→Complete
前端 ─GET /CheckResult→(輪詢) → ─GET /Download→ 算指標+打包 zip 回傳
```

詳細端點契約與內部行為見 [`../specs/smartagentapi-srs.md`](../specs/smartagentapi-srs.md)；用例與驗收見 [`../specs/use-cases-and-acceptance.md`](../specs/use-cases-and-acceptance.md)。

---

## 6. 範圍與限制

- **不在範圍**：AI 推論演算法、外部標註與定量分析、R 風險模型本身（屬 CTMS／外部）；OC 路徑雖存在於 CTMS，但經本 API 入列者固定為 EC。
- **限制**：多處路徑硬編碼（上傳解壓基底、下載暫存以字串取代推導）；`CheckResult` 目錄判斷語意見 SRS。
