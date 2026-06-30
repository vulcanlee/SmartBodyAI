# SmartAgentApi 後端與 AI 推論管線（工程深入）

> 本文件補齊後端 `SmartAgentApi` 的**內部架構與多階段推論管線**細節，與
> [`system-architecture.md`](system-architecture.md) 的高層總覽互補。
> 領域指標公式、風險模型與資料 schema 見
> [`../reference/body-composition-and-risk-models.md`](../reference/body-composition-and-risk-models.md)；
> 對外 API 規格見 [`../reference/api-reference.md`](../reference/api-reference.md)。
>
> **事實來源**：`source/SmartAgentApi/SmartAgentApi/**` 與外部
> `C:\Vulcan\Github\CTMS\src\CTMS\**`（見 [ADR 0005](adr/0005-external-ctms-dependency.md)）。

---

## 1. 專案定位與邊界（重要）

`SmartAgentApi` 是一個**輕量 REST 閘道**，本身**不執行**推論，也**不驅動**推論管線。它只做三件事：

1. 接收前端上傳的 DICOM ZIP、解壓、驗證；
2. 把個案**放進 CTMS 佇列的 InBound**（呼叫 `AgentService.CreateInBound`）；
3. 當結果完成後，讀取 CTMS 完成佇列、計算身體組成指標、打包成 ZIP 供下載。

真正的多階段推論管線（`AgentService.RunAsync`，見第 4 節）是由 **CTMS 端的另一個執行器/主機**反覆呼叫驅動的——`SmartAgentApi` 內**沒有**任何地方呼叫 `RunAsync`，也沒有註冊背景 `HostedService`。

```
┌───────────────────────────┐  上傳ZIP   ┌──────────────────────────────┐
│ SmartBodyAI (前端)          │ ────────▶ │ SmartAgentApi (本專案)         │
│                            │  輪詢/下載 │  Upload / CheckResult / Download│
└───────────────────────────┘ ◀──────── └───────────────┬──────────────┘
                                                          │ CreateInBound（只入列）
                                                          ▼
                                          ┌──────────────────────────────┐
                                          │ CTMS 佇列檔案系統               │
                                          │ InBound→Phase1/2/3→OutBound→   │
                                          │ Complete                       │
                                          └───────────────┬──────────────┘
                                                          ▲ RunAsync（輪詢推進）
                                                          │
                                          ┌──────────────────────────────┐
                                          │ CTMS 端推論執行器（本 repo 之外）│
                                          │ 外部標註 / 定量分析 / Rscript    │
                                          └──────────────────────────────┘
```

> 換言之：上傳成功 ≠ 推論啟動。`SmartAgentApi` 入列後即返回；個案能否往前推進，取決於 CTMS 執行器是否在跑，以及外部標註/定量分析/R 模型是否就緒。

---

## 2. 模組職責

事實來源：`source/SmartAgentApi/SmartAgentApi/`。

| 路徑 | 職責 |
|------|------|
| `Controllers/DicomPackController.cs` | 對外三端點（Upload / CheckResult / Download）與內部 `PushToAICheck`，是後端唯一的進入點。 |
| `Models/BodyAIResult.cs` | 下載時回傳的身體組成結果模型（屬性名以中文後綴，如 `SMA骨骼肌面積`）。 |
| `Models/PatientDataModel.cs` | 本地病患 DTO（`Age/Code/Gender/Height/Weight`）。**目前僅 `PushToAiService` 使用**，主流程不經此模型。 |
| `Services/PushToAiService.cs` | 另一條入列封裝（`Push`）。**未在 `Program.cs` 註冊、未被 controller 使用**，屬目前未啟用的程式碼。 |
| `Program.cs` | 主機與 DI 組態（見下）。 |
| `appsettings.json` | `AgentSetting` 設定區段（鍵與值見 [`configuration-reference.md`](../reference/configuration-reference.md) 第二節）。 |
| `nlog.config` | NLog 輸出（檔案 `C:/temp/logs/SmartAgentApi-logfile-*.log` 等）。 |

### DI 與管線（`Program.cs`）

- 日誌：`UseNLog()`，最低層級 `Trace`。
- `AddControllers()` + `AddOpenApi()`（OpenAPI 僅 Development 啟用）。
- 強型別設定：`Configure<Agentsetting>(... GetSection(MagicObjectHelper.Agentsetting))`（區段名 `Agentsetting`）。
- middleware：`UseHttpsRedirection()` → `UseAuthorization()` → `MapControllers()`。**未啟用**驗證機制（`UseAuthorization` 之前無 `UseAuthentication`），亦無 CORS 設定。
- 註冊的 CTMS 服務（多為 CTMS 管線內部使用）：`AgentService`、`PatientAIInfoService`、`Phase1Phase2Service`、`DirectoryHelperService`、`AIIntegrateService`、`CurrentProject`、`RequestInformation`、`RandomListService`、`RiskAssessmentExcelService`、`InputCsvService`。
  - 注意：`Program.cs` 對 `AgentService`、`PatientAIInfoService`、`Phase1Phase2Service`、`DirectoryHelperService` 同時做了 `AddScoped` 與 `AddTransient` 的**重複註冊**（最後者生效）；屬既有現象，本文件僅如實記載，不建議在無需求時調整。

---

## 3. 對外端點 → 內部動作對應

控制器路由前綴 `/DicomPack`（`[Route("[controller]")]`）。對外規格見 [`api-reference.md`](../reference/api-reference.md)；此處補內部行為。

### 3.1 `POST /DicomPack`（Upload）
1. 驗證有檔案且副檔名為 `.zip`。
2. 暫存基底為**硬編碼**路徑 `C:\temp\SmartBodyAI\TempUploads\DicomPacks`（不取自 `AgentSetting`）。
3. 以 `workId = {yyyyMMddHHmmssfff}_{Guid:N}`（UTC）建立獨立工作目錄並解壓。
4. 呼叫 `PushToAICheck(extractPath)`，最後刪除原始 ZIP（`finally`）。

`PushToAICheck` 行為：
- 讀 `extractPath/PatientData.json` → 反序列化為 CTMS 的 `PatientAIInfo`。
- **`癌別` 硬編碼為 `"EC"`**（子宮內膜癌）；`KeyName = patientAIInfo.Code`（病歷號）。
- 將 `L3CT.dicm` 複製為 `{KeyName}.dicm`。
- 呼叫 `agentService.CreateInBound(patientAIInfo, agentsetting)` 入列 InBound。
- 影響：此 API 只會以 EC 流程入列；OC（卵巢癌）路徑雖存在於 CTMS（見第 4、5 節），但**此 API 不會觸發**。

### 3.2 `GET /DicomPack/CheckResult/{checkKey}`
- 檢查 `{QueueFolderPath}/{CompleteQueueName}/{checkKey}` 是否存在，並對其子目錄做判斷。
- 實際判斷式為：`allDirectories.All(dir => 任一關鍵字 ∈ {Phase1Result,Phase2Result,Phase3Result} 命中 dir)`。
  - 語意是「**每個**子目錄都至少命中三關鍵字之一」，與直覺的「三者**皆存在**」不完全等價（例如只有 `Phase1Result` 一個子目錄時也會回 `true`）。判讀時請以此實作為準。
- 完成回 `200 {Status:true}`，否則 `404 {Status:false}`。

### 3.3 `GET /DicomPack/Download/{checkKey}`
1. 下載暫存目錄 = `DicomFolderPath` 字串將 `Dicom` 取代為 `Temp`（例：`C:\temp\Dicom` → `C:\temp\Temp`）。
2. 若 `{Complete}/{checkKey}` 不存在 → `404`。
3. 讀 `PatientData.json`、以 `AIIntegrateService.GetInputCsv(checkKey, completeQueuePath)` 讀 `Phase3Result/input.csv`（→ `InputCsvModel`）。
4. 依公式計算並填入 `BodyAIResult`（公式逐項見 [body-composition 參考](../reference/body-composition-and-risk-models.md#1-身體組成指標定義與計算公式)），寫出 `BodyAIResult.json`。
5. 將 `{Complete}/{checkKey}` 整個目錄壓成 `{checkKey}.zip` 回傳（`application/zip`）。

---

## 4. 多階段推論管線（CTMS `AgentService.RunAsync`）

事實來源：`CTMS\src\CTMS\AIAgent.Business\Services\AgentService.cs`。`RunAsync` 為**單次輪詢**，依序跑 8 個階段，各階段間 `Task.Delay(150)`（程式碼為 150ms；該方法的 XML 註解誤寫為 500ms，以程式碼為準）。需由外部執行器反覆呼叫才會持續推進。

| # | 方法 | 來源佇列 | 目的佇列 | 動作 / 完成條件 |
|---|------|---------|---------|----------------|
| 1 | `ProceeInBoundAsync` | InBound | Phase1 | 將每個病患資料夾 `Directory.Move` 至 Phase1。 |
| 2 | `ProceePhase1Async` | Phase1 | Phase1Waiting | 讀病患 JSON、複製 DICOM、產生 Phase1「標註生成」設定 JSON、移至 Phase1Waiting。 |
| 3 | `ProceePhase1WaitingAsync` | Phase1Waiting | Phase2 | 輪詢 `Phase1TmpFolder` 同名資料夾；**檔案數 ≥ 2** 視為外部標註完成，複製回 `Phase1Result/` 後移至 Phase2。 |
| 4 | `ProceePhase2Async` | Phase2 | Phase2Waiting | 移至 Phase2Waiting、產生 Phase2「定量分析」設定 JSON。 |
| 5 | `ProceePhase2WaitingAsync` | Phase2Waiting | Phase3 | 輪詢 `Phase2TmpFolder` 同名資料夾；**檔案數 ≥ 23** 視為外部定量分析完成，複製回 `Phase2Result/` 後移至 Phase3。 |
| 6 | `ProceePhase3Async` | Phase3 | Phase3Waiting | 移至 Phase3Waiting。 |
| 7 | `ProceePhase3WaitingAsync` | Phase3Waiting | OutBound | 複製至 OutBound、由 Phase2 結果 CSV 產生 `Phase3Result/input.csv`（依癌別決定欄位）、刪除 Phase3Waiting 個案、呼叫 **Rscript** 產生 `output.csv`。 |
| 8 | `ProceeCompleteAsync` | OutBound | Complete | 若 `Phase3Result/output.csv` 存在，移至 Complete 佇列（供 CheckResult/Download）。 |

### R 腳本執行（階段 7）
透過 `System.Diagnostics.Process` 以 `Rscript` 啟動，工作目錄取自 `AgentSetting`：

- **EC**（`agentsetting.風險評估模型`，預設 `C:\EndometrioidCancer`）：
  `Rscript Run_Endometrioid_Model.R -m Endometrioid_Analysis_20250610_Model_data.RData --varname CaseIn_SMA_Imat_BMI -c 0.5 -i {input.csv} -o {output.csv}`
- **OC**（`agentsetting.風險評估模型OC`，預設 `C:\EndometrioidCancer\OvarianCancer`）：
  `Rscript Run_Ovarian_Model.R -m Ovarian_Analysis_20250908_Model_data.RData -v Case_SMI.BH2_Imat_BMI -d 3 -c 0.5 -i {input.csv} -o {output.csv}`

CSV 欄位格式與輸出判讀見 [body-composition 參考](../reference/body-composition-and-risk-models.md)。

---

## 5. CTMS 相依面（實際消費的型別/方法）

外部專案參考（`SmartAgentApi.csproj`，相對路徑上升 4 層）：
- `..\..\..\..\CTMS\src\CTMS\AIAgent.Business\AIAgent.Business.csproj`
- `..\..\..\..\CTMS\src\CTMS\CTMS.Business\CTMS.Business.csproj`

> 缺少 `CTMS` 原始碼時，`SmartAgentApi` 無法還原/建置（[ADR 0005](adr/0005-external-ctms-dependency.md)）。

### 5.1 SmartAgentApi 直接呼叫的 CTMS 面（窄）
| 命名空間 / 型別 | 成員 | 用途 |
|----------------|------|------|
| `AIAgent.Services.AgentService` | `CreateInBound(PatientAIInfo, Agentsetting)` | 入列 InBound（`PushToAICheck` 唯一呼叫）。 |
| `CTMS.Business.Services.ClinicalInformation.AIIntegrateService` | `GetInputCsv(checkKey, rootPath)` | 讀 `Phase3Result/input.csv` → `InputCsvModel`（`Download` 唯一呼叫）。 |
| `AIAgent.Models.Agentsetting` | 屬性 + `Get*QueuePath()` | 佇列路徑組合與設定值。 |
| `CTMS.DataModel.Models.AIAgent.PatientAIInfo` | `Code/KeyName/Height/癌別/DicomFilename...` | 病患資料容器（由 `PatientData.json` 反序列化）。 |
| `CTMS.DataModel.Models.AIAgent.InputCsvModel` | `Total_SMD/Total_SMG/Total_ImatA/Total_LamaA/Total_NamaA...` | 指標計算來源。 |
| `CTMS.Share.Helpers.MagicObjectHelper` | `Agentsetting` 等常數 | 設定區段名等。 |
| `CTMS.Share.Extensions.StringExtensions` | `ToFloat()` | 字串轉數值。 |

> 注意：`AIIntegrateService` 另有 `CheckAIProcess`、`GetOnputCsv`（風險判讀）等方法，但 `SmartAgentApi` **未使用**——CheckResult 是自行以目錄判斷，Download 不讀 `output.csv`。風險判讀（`output.csv` 是否含 `a grade III AE`）由 CTMS 平台端消費，不在本 API。

### 5.2 推論管線內部使用的 CTMS 面（由 CTMS 執行器驅動，非本 API）
`AgentService.RunAsync` / `Procee*Async`、`PatientAIInfoService.ReadAsync`、`Phase1Phase2Service`（`CopyDicomAsync`、`BuildPhase1標註生成Json`、`MoveToPhaseN*Async`、`CopyToOutBoundAsync` 等）、`DirectoryHelperService.CopyDirectory`、`RiskAssessmentExcelService.ReadExcel`、`InputCsvService.Read`，以及 `SyncExcel.Services` 命名空間下的服務。

---

## 6. 檔案系統佇列佈局

根目錄 `QueueFolderPath`（預設 `C:\temp\CTMS\Queue`）下，每個個案以 `{KeyName}` 子目錄貫穿各階段。**注意 `Phase1TmpFolder` / `Phase2TmpFolder` 是獨立根目錄**（`C:\CanWellBeing\temp1`、`temp2`），非 `QueueFolderPath` 子目錄。

```
QueueFolderPath (C:\temp\CTMS\Queue)
├─ InBound\{KeyName}\        PatientData.json, {KeyName}.dcm
├─ Phase1\{KeyName}\
├─ Phase1Waiting\{KeyName}\  …\Phase1Result\（外部標註結果複製回）
├─ Phase2\{KeyName}\
├─ Phase2Waiting\{KeyName}\  …\Phase2Result\{KeyName}.csv（外部定量分析）
├─ Phase3\{KeyName}\
├─ Phase3Waiting\{KeyName}\
├─ OutBound\{KeyName}\       Phase3Result\input.csv（生成）, output.csv（Rscript）
└─ Complete\{KeyName}\       Phase1/2/3Result\…, PatientData.json, BodyAIResult.json（下載時生成）

獨立暫存根目錄（外部程序寫入、本管線輪詢）：
  Phase1TmpFolder  C:\CanWellBeing\temp1\{KeyName}\   （檔案數 ≥ 2 視為完成）
  Phase2TmpFolder  C:\CanWellBeing\temp2\{KeyName}\   （檔案數 ≥ 23 視為完成）

其他：
  DicomFolderPath  C:\temp\Dicom\{KeyName}.dcm
  下載暫存          C:\temp\Temp\{checkKey}.zip （DicomFolderPath 的 "Dicom"→"Temp"）
  上傳解壓暫存      C:\temp\SmartBodyAI\TempUploads\DicomPacks\{workId}\ （硬編碼）
```

---

## 7. 一次完整推論的時序

```
前端 ─POST /DicomPack(zip)→ SmartAgentApi
        └ 解壓 → PushToAICheck → AgentService.CreateInBound → InBound\{KeyName}
        └ 回 200 {WorkId}（此時推論尚未啟動）

CTMS 執行器（外部）反覆 RunAsync：
  InBound→Phase1→(外部標註, Tmp1≥2)→Phase2→(外部定量分析, Tmp2≥23)
        →Phase3→(產生 input.csv + Rscript→output.csv)→OutBound→Complete

前端 ─GET /CheckResult/{checkKey}→（輪詢，直到 Status=true）
前端 ─GET /Download/{checkKey}→ 讀 input.csv→算指標→寫 BodyAIResult.json→打包 zip 回傳
```

---

## 8. 已知注意事項

- **閘道 vs 引擎**：本 API 不驅動管線（第 1 節）；除錯「個案卡住」時，先確認 CTMS 執行器與外部標註/定量分析/R 環境是否運作。
- **EC 硬編碼**：`PushToAICheck` 固定 `癌別="EC"`，本 API 不會走 OC 路徑。
- **路徑硬編碼**：上傳解壓基底 `C:\temp\SmartBodyAI\TempUploads\DicomPacks` 未取自設定；下載暫存以字串取代 `Dicom`→`Temp` 推導。
- **重複 DI 註冊**、`PushToAiService` / 本地 `PatientDataModel` 目前未被主流程使用（第 2 節）。
- **開發環境略過 SSL 憑證驗證**（見 CHANGELOG 2026/02），正式環境須恢復驗證。
- `CheckResult` 的目錄判斷語意見 3.2，與直覺略有差異。
