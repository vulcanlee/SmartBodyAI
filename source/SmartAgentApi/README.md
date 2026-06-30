# SmartAgentApi（後端 API）

ASP.NET Core **WebAPI**，DICOM 與 AI 整合的**輕量 REST 閘道**：接收前端上傳的 DICOM 壓縮包、將個案入列 CTMS 推論佇列、並在結果完成後彙整身體組成指標供下載。

> **重要邊界**：本服務**不驅動**多階段推論管線——只把個案放進 `InBound`；管線由 CTMS 端的外部執行器驅動。詳見 [`docs/architecture/smartagentapi-inference-pipeline.md`](../../docs/architecture/smartagentapi-inference-pipeline.md)。

## 端點

| 方法 | 路由 | 用途 |
|------|------|------|
| POST | `/DicomPack` | 上傳 DICOM ZIP（含 `PatientData.json`+`L3CT.dicm`），解壓並入列 |
| GET | `/DicomPack/CheckResult/{checkKey}` | 查詢 AI 結果是否完成 |
| GET | `/DicomPack/Download/{checkKey}` | 計算指標、打包並下載結果 ZIP |

## 外部相依（建置前提）

參考 repo 外部的 `CTMS` 專案：

- `..\..\..\..\CTMS\src\CTMS\AIAgent.Business\AIAgent.Business.csproj`
- `..\..\..\..\CTMS\src\CTMS\CTMS.Business\CTMS.Business.csproj`

**缺少 `CTMS` 原始碼時無法還原/建置**（見 [約束 C-10](../../docs/guidelines/constraints.md)、[ADR 0005](../../docs/architecture/adr/0005-external-ctms-dependency.md)）。風險評估階段另需 R（`Rscript`）執行環境。

## 建置與執行

```powershell
# 於 source/SmartAgentApi/SmartAgentApi（須先備妥 CTMS 相依）
dotnet run
# 預設 https://localhost:7274（http 5127）
```

## 設定

設定於 `SmartAgentApi/appsettings.json`（`Agentsetting`）：佇列根目錄、各 Phase 佇列名、暫存資料夾、推論路徑、EC/OC 風險模型路徑等。完整鍵值見 [設定參考第二節](../../docs/reference/configuration-reference.md)。

## 文件

- 產品需求：[`docs/prd/smartagentapi-prd.md`](../../docs/prd/smartagentapi-prd.md)
- 功能規格：[`docs/specs/smartagentapi-srs.md`](../../docs/specs/smartagentapi-srs.md)
- 推論管線與 CTMS 相依面：[`docs/architecture/smartagentapi-inference-pipeline.md`](../../docs/architecture/smartagentapi-inference-pipeline.md)
- 指標公式與資料 schema：[`docs/reference/body-composition-and-risk-models.md`](../../docs/reference/body-composition-and-risk-models.md)
- 文件總索引：[`docs/README.md`](../../docs/README.md)
