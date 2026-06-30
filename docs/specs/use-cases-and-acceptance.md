# 使用者情境與驗收條件（Use Cases & Acceptance Criteria）

> 本文件以使用案例（UC）與 Given-When-Then 驗收條件描述系統行為，涵蓋前端、後端與端到端整合。
> 每條驗收引用對應需求編號（`FE-FR`/`BE-FR`，見 [`../prd/`](../prd/)），規格細節見 [`./`](.)（SRS）。

---

## 前端 SmartBodyAI

### UC-01 SMART 啟動與授權（FE-FR-01/02/03/04/05）
- **角色**：開發者 / 操作者　**前置**：可用的 FHIR Sandbox 與授權設定。
- **AC-01.1**：Given 由 launcher 帶入 `iss`/`launch`，When 進入 `/launch`，Then 完成 Discovery 並導向授權伺服器，URL 含 `code_challenge`（S256）、`state`、一致的 `redirect_uri`。
- **AC-01.2**：Given Discovery 取不到 well-known，When 啟動，Then fallback 至 `/metadata` 並仍能取得授權/Token 端點；皆失敗則顯示錯誤、不導向。
- **AC-01.3**：Given callback 帶回的 `state` 與保存值不符，When 進入 `/patient-information`，Then 判定失敗並清理 state，不交換 token。
- **AC-01.4**：Given 有效 `code`+`state`，When 交換 token，Then 驗證 `token_type=Bearer`、`access_token` 與 patient context 存在，否則返回首頁。

### UC-02 讀取並呈現病患資料（FE-FR-06）
- **AC-02.1**：Given 已取得 Bearer token 與 patient context，When 讀取 Patient，Then 顯示姓名/性別/生日/身高/體重，且 Patient 不為 null。
- **AC-02.2**：Given 病患有 vital-signs Observation，When 查詢身高/體重，Then 正確呈現數值與單位。

### UC-03 健康檢查就緒（FE-FR-09）
- **AC-03.1**：Given 設定缺必要欄位或目錄無寫入權限，When 開啟 `/health-check`，Then 對應指標顯示黃/紅燈並提供建議，總分反映扣分。
- **AC-03.2**：Given 以 `?iss=` 覆寫，When 產生報告，Then 來源診斷標示該欄位來自 query 並與 appsettings 差異。

### UC-04 SMART Checklist 審查（FE-FR-10）
- **AC-04.1**：Given 任一必要項為 Red，When 計分，Then 結論為「難以通過」。
- **AC-04.2**：Given 編輯後匯出 JSON 再匯入，When 還原，Then 各項 `Status`/`TestResult`/`FailureReason`/`ImprovementSuggestion` 與匯出時一致（快照版本=1）。

---

## 後端 SmartAgentApi

### UC-05 上傳 DICOM ZIP（BE-FR-01/02/03）
- **AC-05.1**：Given 合法 `.zip`（含 `PatientData.json`+`L3CT.dicm`），When `POST /DicomPack`，Then 回 `200` 與 `WorkId`，並於 `InBound` 建立個案（`癌別=EC`、`KeyName=Code`）。
- **AC-05.2**：Given 非 `.zip` 或空檔，When 上傳，Then 回 `400`。
- **AC-05.3**：Given 壞損 ZIP，When 解壓，Then 回 `400`（非有效 ZIP）。

### UC-06 查詢結果狀態（BE-FR-04）
- **AC-06.1**：Given `Complete/{checkKey}` 不存在，When `GET /CheckResult/{checkKey}`，Then 回 `404 Status:false`。
- **AC-06.2**：Given 完成目錄子目錄皆命中 Phase1/2/3Result 關鍵字，When 查詢，Then 回 `200 Status:true`。（判斷語意見 [SRS S-2](smartagentapi-srs.md)）

### UC-07 下載結果與指標（BE-FR-05）
- **AC-07.1**：Given 完成目錄含 `Phase3Result/input.csv` 與 `PatientData.json`，When `GET /Download/{checkKey}`，Then 產生 `BodyAIResult.json`（SMA=LamaA+NamaA、SMI=(LamaA+NamaA)/(身高m)²、Myosteatosis=ImatA+LamaA，皆 F2）並回傳 `{checkKey}.zip`。
- **AC-07.2**：Given 完成目錄不存在，When 下載，Then 回 `404`。

---

## 端到端整合（E2E，跨前後端）

### UC-08 由啟動到取得 AI 結果
- **前置**：前後端皆啟動（7170 / 7274）、CTMS 外部執行器運作、R 環境就緒。
- **流程/驗收**：
  1. **AC-08.1**：完成 UC-01 授權並讀到病患（UC-02）。
  2. **AC-08.2**：於病患頁上傳 DICOM ZIP，前端取得 `WorkId`（對應 UC-05）。
  3. **AC-08.3**：前端輪詢 `CheckResult` 直到 `Status:true`（推論由 CTMS 外部執行器推進 InBound→Phase1/2/3→Complete）。
  4. **AC-08.4**：前端 `Download` 取得結果 ZIP 並於 `/AIResult/{RandomCode}` 呈現身體組成指標與影像。
  5. **AC-08.5**：進度列依序點亮 5 個步驟（`ProcessStepEnum`），過程可中止。
- **負向**：
  - **AC-08.6**：Given CTMS 執行器未運作，When 持續輪詢，Then `CheckResult` 維持未完成；前端不應誤判完成（上傳成功 ≠ 推論啟動）。

---

> 備註：本文件為人工/自動驗收依據；自動化測試現況見 [`../operations/second-stage-testing-checklist.md`](../operations/second-stage-testing-checklist.md) 與 `SmartBodyAI.Tests`。
