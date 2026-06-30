# 資料字典與介面規格（Data Dictionary & Interfaces）

> 本文件彙整兩系統的**資料模型**與**介面契約**。後端 AI 資料 schema（`PatientData.json`、
> `input.csv`、`output.csv`、`BodyAIResult.json` 等）已於
> [`body-composition-and-risk-models.md`](body-composition-and-risk-models.md) 詳列，此處**指向**不重複；
> 對外 API 端點見 [`api-reference.md`](api-reference.md)；設定鍵見 [`configuration-reference.md`](configuration-reference.md)。
>
> 事實來源：`source/SmartBodyAI/SmartBodyAI/Models/*`，已逐一核實。

---

## 1. 前端設定模型

### `SettingModel`（appsettings 繫結，`SmartAppSetting` 區段）
`SystemInformation`、`FhirServerUrl`、`RedirectUrl`、`ClientId`、`ClientSecret?`、`InferenceHostApi`、`AuthorizationScope`、`ProcessDelayTimeInMilliSeconds`、`UploadDicomPath`、`UploadDicomTempPath`、`DicomImagePath`、`AIResultPath`、`IsDebug`。

### `SmartAppSettingModel`（執行時 SMART 狀態）
繼承設定值並承載授權流程狀態：`IsDebug`、`AuthorizationScope`、`FhirServerUrl`、`InferenceHostApi`、`AIResultPath`、`ClientId`、`ClientSecret?`、`RedirectUrl`、`AuthCode`、`CodeVerifier`、`CodeChallengeMethod`（預設 `S256`）、`AuthorizationError(/Description)`、`ClientState`（預設 `local_state`）、`TokenUrl`、`RefreshTokenUrl`、`AuthorizeUrl`、`Iss`、`Launch`、`State`。常數 `DefaultCodeChallengeMethod="S256"`。

---

## 2. 前端授權/Discovery 模型

| 模型 | 欄位（重點） |
|------|------|
| `SmartDiscoveryResult` | `AuthorizeUrl`、`TokenUrl`、`MetadataSource`、`Capabilities[]`、`ValidationErrors[]`；`IsValid`=有端點且無錯誤 |
| `SmartAuthorizationRequest` | 授權 URL 與 state（送出授權請求用） |
| `SmartAuthorizationRequestContext` | 建立授權請求的輸入上下文 |
| `SmartCallbackValidationResult` | callback（code/state/error）驗證結果 |
| `SmartTokenValidationResult` | `AccessToken`、`PatientId`、`Scopes`、`IdToken`、`FhirUser`、`Errors[]`；`IsValid`=四值齊備且無錯誤 |
| `SmartResponse` | SMART token 回應反序列化（`access_token`/`patient`/`token_type`/`scope`/`id_token`/`fhirUser`/`expires_in`/`refresh_token` 等，含 `JsonPropertyName`） |

---

## 3. 前端病患/流程模型

| 模型 | 欄位 |
|------|------|
| `PatientInformationModel` | `Id`、`Identifier`、`Name`、`BirthDate`、`Gender`、`HeightValue/Unit`、`WeightValue/Unit`；含 `GetAge()`/`GetAgeDescription()`/`GetHeight/Weight*` 衍生方法 |
| `PatientDataModel`（前端） | `Age`、`Code`、`Gender`、`Height`、`Weight`（**送往後端的 `PatientData.json` 來源**） |
| `ProcessModel` | `ActiveClass[]`（4 槽）+ 具名欄位 `確認基本資料/上傳DICOM/進行AI推論/查看AI分析` |
| `ProcessStepEnum` | `確認基本資料=0`、`上傳DICOM=1`、`進行AI推論=2`、`查看AI分析=3`、`查看結果=4` |

> 注意：前端 `PatientDataModel` 與後端反序列化用的 CTMS `PatientAIInfo` 欄位不完全相同；兩端以 `PatientData.json` 為介面，詳見 [body-composition §3.1](body-composition-and-risk-models.md)。

---

## 4. 健康檢查與 Checklist 模型

| 模型 | 欄位（重點） |
|------|------|
| `HealthCheckSummary` | `CheckedAt`、`SystemName/Version`、`TotalScore`、`Green/Yellow/RedCount`、`EffectiveFhirServerUrl`、`ResolvedAuthorize/TokenUrl`、`FhirMetadataSource`、`Indicators[]`、`ConfigurationFields[]`、`Highlights[]`、`BoundSetting(Json)`、`RuntimeSetting(Json)` |
| `HealthIndicatorResult` | `Key`、`Title`、`Status`、`Summary`、`Details`、`Recommendation`、`Source/EffectiveValue`、`ProviderName`；`Score`：Green=100/Yellow=50/Red=0 |
| `HealthIndicatorStatus` | enum `Green`/`Yellow`/`Red` |
| `ConfigurationFieldDiagnostic` | 設定欄位來源診斷（appsettings/env/runtime/query） |
| `SmartChecklistPageModel` | `Title`、`Sections[]`、`TotalScore`、`Green/Yellow/RedCount`、`OverallStatusLabel`、`KeyRisks[]`、`FinalRecommendations[]`；`Recalculate()` 加權計分與判讀 |
| `SmartChecklistSection` | 分類（`Items[]`、`Score`、各色計數） |
| `SmartChecklistItem` | `Key`、`Title`、`IsRequired`、`Weight`、`Status`、`WhyItMatters`、`Rule`、`TestResult`、`FailureReason`、`ImprovementSuggestion`；`Score`：Green=100/Yellow=60/Red=0 |
| `SmartChecklistSnapshot`(+`Item`) | JSON 匯出快照（`Version`=1、`Title`、`ExportedAt`、`Items[]`） |

> 計分差異：健康檢查指標 Yellow=**50**；checklist 項目 Yellow=**60**。判讀規則見 [SRS S-6/S-7](../specs/smartbodyai-srs.md)。

---

## 5. 介面契約

### 5.1 前端 → FHIR 伺服器
Bearer token 呼叫 `Hl7.Fhir` `FhirClient`，讀取 Patient、Observation（vital-signs 身高/體重）、Encounter、MedicationRequest、ServiceRequest（見 [system-architecture §3.1](../architecture/system-architecture.md)）。

### 5.2 前端 → SmartAgentApi
`POST /DicomPack`、`GET /DicomPack/CheckResult/{checkKey}`、`GET /DicomPack/Download/{checkKey}`；請求/回應契約見 [`api-reference.md`](api-reference.md)，內部行為見 [SRS（後端）](../specs/smartagentapi-srs.md)。

### 5.3 後端 ↔ CTMS 檔案佇列
以檔案系統佇列交換（`InBound`→`Phase1/2/3`→`OutBound`→`Complete`），交換物件 `PatientData.json`、DICOM、`input.csv`/`output.csv`、各 `PhaseNResult/`；佈局與相依面見 [inference-pipeline §5/§6](../architecture/smartagentapi-inference-pipeline.md)。

### 5.4 資料 schema（指向）
`PatientData.json`、Phase2 `{KeyName}.csv`、`input.csv`（EC/OC）、`output.csv`、`BodyAIResult.json` 的完整欄位見 [`body-composition-and-risk-models.md` §3](body-composition-and-risk-models.md)。
