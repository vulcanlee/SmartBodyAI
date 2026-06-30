# 身體組成指標與風險模型參考

> 本文件彙整 AI 推論流程中的**領域知識與資料 schema**：身體組成指標的定義與計算公式、
> EC/OC 兩癌別的風險模型與 R 腳本，以及各階段交換的 JSON / CSV 結構。
> 後端流程與佇列見 [`../architecture/smartagentapi-inference-pipeline.md`](../architecture/smartagentapi-inference-pipeline.md)。
>
> **事實來源**：`SmartAgentApi\Controllers\DicomPackController.cs`、`SmartAgentApi\Models\BodyAIResult.cs`、
> `CTMS\...\AgentService.cs`、`CTMS\...\InputCsvModel.cs`、`CTMS\...\AIIntegrateService.cs`。
> 凡公式皆對照原始碼確認。

---

## 1. 身體組成指標定義與計算公式

下載端點（`DicomPackController.Download`）讀取 `Phase3Result/input.csv`（→ `InputCsvModel`）與
`PatientData.json`，計算下列指標寫入 `BodyAIResult.json`。所有數值皆格式化為小數 2 位（`ToString("F2")`）。

| 指標（`BodyAIResult` 屬性） | 英文 | 計算方式（來源欄位來自 `InputCsvModel`） |
|------|------|------|
| `SMD骨骼肌密度` | Skeletal Muscle Density | `Total_SMD` |
| `SMG骨骼肌綜合指標` | Skeletal Muscle Gauge | `Total_SMG` |
| `IMAT肌間肌肉脂肪組織` | Intermuscular Adipose Tissue | `Total_ImatA` |
| `LAMA低密度肌肉區域` | Low Attenuation Muscle Area | `Total_LamaA` |
| `NAMA正常密度肌肉區域` | Normal Attenuation Muscle Area | `Total_NamaA` |
| `SMA骨骼肌面積` | Skeletal Muscle Area | **`Total_LamaA + Total_NamaA`** |
| `SMI骨骼肌指標` | Skeletal Muscle Index | **`(Total_LamaA + Total_NamaA) / (身高_公尺)²`**，身高 = `PatientData.Height / 100` |
| `Myosteatosis肌肉脂肪變性` | Myosteatosis | **`Total_ImatA + Total_LamaA`** |

- `SMI` 的身高來自 `PatientData.json` 的 `Height`（公分），程式內換算為公尺平方作分母。
- `ImagePng` 固定組成 `"{checkKey}/Phase1Result/{checkKey}.png"`。

> 這些指標**僅由下載端點即時計算**；CTMS 佇列中的 `input.csv` 本身不含 `SMA/SMI/Myosteatosis` 等衍生欄位。

---

## 2. EC / OC 風險模型與 R 腳本

階段 7（`ProceePhase3WaitingAsync`）依 `PatientAIInfo.癌別` 產生 `input.csv` 並呼叫對應 R 模型。
注意 **`SmartAgentApi` 入列時將 `癌別` 硬編碼為 `EC`**，故經由本 API 的個案只會走 EC；OC 路徑保留供 CTMS 其他入口使用。

### 2.1 子宮內膜癌（EC）
- 模型目錄：`AgentSetting.風險評估模型`（預設 `C:\EndometrioidCancer`）。
- `input.csv` 表頭（13 欄）：
  ```
  ID,Age,Tumor.Grade,body.height.cm,body.weight.kg,Vertebral.Body.Area.cm2,Total.SMD,Total.ImatA,Total.LamaA,Total.NamaA,VatA,SatA,Total.SMG
  ```
  - `Age` 去除小寫 `y` 後寫入；`Tumor.Grade` 目前固定為 `"1"`（程式內 `tumorGrade`，尚未由病患資料推導）。
- R 命令：
  ```
  Rscript Run_Endometrioid_Model.R -m Endometrioid_Analysis_20250610_Model_data.RData \
          --varname CaseIn_SMA_Imat_BMI -c 0.5 -i {input.csv} -o {output.csv}
  ```

### 2.2 卵巢癌（OC）
- 模型目錄：`AgentSetting.風險評估模型OC`（預設 `C:\EndometrioidCancer\OvarianCancer`）。
- `input.csv` 表頭（12 欄）：
  ```
  ID,Body.Height.cm,Body.Weight.kg,SMA,SMD,ImatA,LamaA,NamaA,MyosteatosisA,VatA,SatA,Total.SMG
  ```
  - `Body.Height.cm` 以 `BodyHeight × 100` 計算（來源身高視為公尺）；
    `SMA = LamaA + NamaA`；`MyosteatosisA = ImatA + LamaA`。

### 2.3 輸出判讀（`output.csv`）
`AIIntegrateService.GetOnputCsv` 讀 `output.csv` 全文（轉小寫）判讀：
- 含 `a grade III AE` → **高風險**（建議減量「需要」）；
- 否則 → **低風險**（「不需要」）。

> 此判讀由 **CTMS 平台端**消費；`SmartAgentApi` 的 Download/CheckResult **不讀** `output.csv`，僅以目錄存在與否判斷完成。

---

## 3. 資料 schema

### 3.1 `PatientData.json`（上傳 ZIP 內 + 各佇列內）
反序列化為 CTMS `PatientAIInfo`。本流程實際讀寫的欄位：

| 欄位 | 說明 |
|------|------|
| `Code` | 病歷號；`PushToAICheck` 以此設定 `KeyName` |
| `Age` / `Height` / `Weight` / `Gender` | 病患基本資料（`Height` 公分，供 SMI 計算） |
| `SubjectCode` | 受試者代碼（選用） |
| `癌別` | `EC` / `OC`；經本 API 入列時被覆寫為 `EC` |
| `KeyName` | 個案唯一鍵（本 API = `Code`；CTMS 其他入口以 `InitKeyName()` 產生時間戳鍵） |
| `DicomFilename` / `DestionatioDicomFilename` / `DestionatioPatientJSONFilename` | DICOM 與 JSON 的來源/目的路徑 |

上傳 ZIP 內另需含 DICOM 影像 `L3CT.dicm`。

### 3.2 Phase2 結果 `{KeyName}.csv`
由外部定量分析產生，置於 `Phase2Result\{KeyName}.csv`，經 `RiskAssessmentExcelService.ReadExcel` 解析。
階段 7 產生 `input.csv` 時讀取下列欄位：`ID`、`Age`、`BodyHeight`、`BodyWeight`、`VertebralBodyAreaCm2`、
`TotalSMD`、`TotalImatA`、`TotalLamaA`、`TotalNamaA`、`VatA`、`SatA`、`TotalSMG`。

### 3.3 `Phase3Result/input.csv`（→ `InputCsvModel`）
`InputCsvModel` 欄位（`InputCsvService.Read` 解析）：
`ID`、`Age`、`Tumor_Grade`、`body_height_cm`、`body_weight_kg`、`Vertebral_Body_Area_cm2`、
`Total_SMD`、`Total_SMG`、`Total_ImatA`、`Total_LamaA`、`Total_NamaA`、`VatA`、`SatA`、`Myosteatosis`。
下載端點實際使用：`Total_SMD`、`Total_SMG`、`Total_ImatA`、`Total_LamaA`、`Total_NamaA`。

### 3.4 `Phase3Result/output.csv`
由 R 模型輸出；格式由模型決定。判讀規則見 2.3。

### 3.5 `BodyAIResult.json`（下載回傳）
`BodyAIResult` 模型欄位：

| 欄位 | 說明 |
|------|------|
| `SMA骨骼肌面積`、`SMI骨骼肌指標`、`SMD骨骼肌密度`、`SMG骨骼肌綜合指標` | 主要骨骼肌指標（見第 1 節） |
| `IMAT肌間肌肉脂肪組織`、`LAMA低密度肌肉區域`、`NAMA正常密度肌肉區域`、`Myosteatosis肌肉脂肪變性` | 脂肪/肌肉組成指標 |
| `ImagePng` | Phase1 結果影像相對路徑 `{KeyName}/Phase1Result/{KeyName}.png` |
| `CTImage1`、`CTImage2`、`Image`、`ImageExtension` | 影像欄位（目前下載流程未填值） |
| `ImageDicom` | 唯讀計算屬性 `{Image}.dicm` |

> 前端 `SmartBodyAI` 取得下載 ZIP 後，於 AI 結果頁呈現上述指標與影像。
