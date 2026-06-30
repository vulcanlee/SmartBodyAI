# 功能規格書（SRS）— SmartAgentApi（後端）

> 本文件為後端 `SmartAgentApi` 的細部功能規格，逐端點與內部行為描述契約、邊界與錯誤處理，
> 對應 [`../prd/smartagentapi-prd.md`](../prd/smartagentapi-prd.md) 的 `BE-FR` 需求編號。
> 對外端點摘要見 [`../reference/api-reference.md`](../reference/api-reference.md)；內部管線與 CTMS 相依面見
> [`../architecture/smartagentapi-inference-pipeline.md`](../architecture/smartagentapi-inference-pipeline.md)（本文件不重複管線細節）。
>
> 事實來源：`source/SmartAgentApi/SmartAgentApi/Controllers/DicomPackController.cs` 等，已逐行核實。

---

## S-1（BE-FR-01/02）上傳 DICOM ZIP — `POST /DicomPack`

- **請求**：`multipart/form-data`，欄位 `file`（`IFormFile`）；`[RequestSizeLimit(1_000_000_000)]`（約 1 GB）。
- **前置/驗證**：`file` 非空、副檔名 `.zip`（不分大小寫）。
- **行為**：
  1. 暫存基底為**硬編碼** `C:\temp\SmartBodyAI\TempUploads\DicomPacks`（不取自 `AgentSetting`），不存在則建立。
  2. `workId = {yyyyMMddHHmmssfff(UTC)}_{Guid:N}`；存 `{workId}.zip` 後 `ZipFile.ExtractToDirectory` 解壓至 `{workId}\`。
  3. 呼叫 `PushToAICheck(extractPath)`（見 S-4）；`finally` 刪除原始 ZIP。
- **回應**：
  - `200`：`{ Message:"上傳並解壓縮成功。", WorkId, ExtractPath }`
  - `400`：未收到檔案/空檔/非 `.zip`/非有效 ZIP（`InvalidDataException`）
  - `500`：其他例外 `{ Message:"處理 ZIP 檔案時發生錯誤。", Error }`
- **前置內容**：ZIP 內須含 `PatientData.json` 與 `L3CT.dicm`。

## S-2（BE-FR-04）查詢結果狀態 — `GET /DicomPack/CheckResult/{checkKey}`

- **行為**：檢查 `{QueueFolderPath}/{CompleteQueueName}/{checkKey}` 是否存在並取其子目錄。
- **判斷式**：`allDirectories.All(dir => 任一 ∈ {Phase1Result,Phase2Result,Phase3Result} 命中 dir)`。
  - 語意為「**每個**子目錄都至少命中三關鍵字之一」，**非**「三者皆存在」（例如僅 `Phase1Result` 一個子目錄時亦回 `true`）。判讀以此實作為準。
- **回應**：`200 {Status:true,...}`（已完成）／`404 {Status:false, Message:"尚未完成 AI 推論，請稍後再試。"}`（目錄不存在、無子目錄、或未全數命中）。

## S-3（BE-FR-05）結果下載與指標計算 — `GET /DicomPack/Download/{checkKey}`

- **前置**：`{Complete}/{checkKey}` 須存在，否則 `404`。
- **行為**：
  1. 下載暫存目錄 = `DicomFolderPath` 將字串 `Dicom` 取代為 `Temp`（例 `C:\temp\Dicom`→`C:\temp\Temp`）。
  2. 讀 `PatientData.json`（→ `PatientAIInfo`）。
  3. `AIIntegrateService.GetInputCsv(checkKey, completeQueuePath)` 讀 `Phase3Result/input.csv`（→ `InputCsvModel`）。
  4. 計算指標寫入 `BodyAIResult`（公式見 [body-composition §1](../reference/body-composition-and-risk-models.md)；皆 `F2`）：SMD/SMG/IMAT/LAMA/NAMA 取對應欄位、SMA=`LamaA+NamaA`、SMI=`(LamaA+NamaA)/(身高m)²`、Myosteatosis=`ImatA+LamaA`；`ImagePng = {checkKey}/Phase1Result/{checkKey}.png`。
  5. 寫 `BodyAIResult.json` → 將 `{Complete}/{checkKey}` 整個目錄壓成 `{checkKey}.zip`。
- **回應**：`200` `application/zip`（檔名 `{checkKey}.zip`）；`404`（目錄不存在）。

## S-4（BE-FR-03）入列 CTMS — 內部 `PushToAICheck(extractPath)`

- **行為**：讀 `extractPath/PatientData.json`（→ `PatientAIInfo`）；**`癌別` 固定 `"EC"`**、`KeyName = Code`；將 `L3CT.dicm` 複製為 `{KeyName}.dicm`；呼叫 `AgentService.CreateInBound(patientAIInfo, agentsetting)` 入列 `InBound`。
- **邊界**：入列即完成，**不**啟動推論；管線由 CTMS 外部執行器驅動（[BE-NFR-02](../prd/smartagentapi-prd.md)）。OC 路徑不會經此 API 觸發。

## S-5（BE-FR-06）日誌與設定

- **設定**：`Configure<Agentsetting>(GetSection("Agentsetting"))`；鍵值見 [`configuration-reference.md` 第二節](../reference/configuration-reference.md)。
- **DI**：`AgentService`、`AIIntegrateService` 等 CTMS 服務（含重複註冊，見 [inference-pipeline §2](../architecture/smartagentapi-inference-pipeline.md)）。`PushToAiService` 與本地 `PatientDataModel` 目前未被主流程使用。
- **middleware**：`UseHttpsRedirection` → `UseAuthorization` → `MapControllers`；OpenAPI 僅 Development；**未啟用驗證/CORS**。
- **日誌**：NLog 記錄上傳、解壓、入列、查詢、下載節點（[C-9](../guidelines/constraints.md)）。

---

## 錯誤碼摘要

| 端點 | 200 | 400 | 404 | 500 |
|------|-----|-----|-----|-----|
| `POST /DicomPack` | 解壓+入列成功 | 無檔/非 zip/壞 zip | — | 其他例外 |
| `GET /CheckResult/{key}` | 已完成 | — | 未完成/目錄不存在 | — |
| `GET /Download/{key}` | zip 下載 | — | 完成目錄不存在 | — |
