# SmartBodyAI（前端主站）

ASP.NET Core **Blazor Server** 主站，SMART on FHIR 病患應用前端：負責 SMART 啟動/授權、病患 FHIR 資料呈現、DICOM 上傳介面、AI 結果呈現，以及健康檢查與合規 checklist。AI 推論與 DICOM 處理委派後端 [`SmartAgentApi`](../SmartAgentApi/README.md)。

## 專案內容

| 路徑 | 說明 |
|------|------|
| `SmartBodyAI/` | Blazor Server 主站專案 |
| `SmartBodyAI.Tests/` | xUnit 測試專案（Discovery／授權／checklist／健檢） |
| `SmartBodyAI.slnx` | 方案檔 |

## 技術棧

.NET 10.0、Blazor Server（Interactive Server）、HL7 FHIR R4（`Hl7.Fhir.R4`）、fo-dicom + ImageSharp、Ant Design Blazor、Syncfusion Blazor、NLog、`System.IdentityModel.Tokens.Jwt`。

## 建置與執行

```powershell
# 於 source/SmartBodyAI/SmartBodyAI
dotnet run
# 預設 https://localhost:7170，首頁進入 SMART 啟動流程
```

主要頁面：`/`（啟動）、`/launch`、`/patient-information`（callback）、`/health-check`、`/smart-checklist`、`/AIResult/{RandomCode}`。

測試：

```powershell
dotnet test source/SmartBodyAI/SmartBodyAI.Tests/SmartBodyAI.Tests.csproj
```

## 設定與機密

設定於 `SmartBodyAI/appsettings.json`（`SmartAppSetting`）；`ClientSecret` 留空，以環境變數或 User Secrets 注入（見 [約束 C-1](../../docs/guidelines/constraints.md)、[設定參考](../../docs/reference/configuration-reference.md)）。

## 關鍵目錄

`Components/Pages`（路由頁）、`Components/Views`（流程元件）、`Services`（SMART/健檢/checklist 邏輯）、`Servicers`（設定/state/DICOM 基礎服務）、`Models`、`Helpers`（`PkceHelper` 等）。

## 文件

- 產品需求：[`docs/prd/smartbodyai-prd.md`](../../docs/prd/smartbodyai-prd.md)
- 功能規格：[`docs/specs/smartbodyai-srs.md`](../../docs/specs/smartbodyai-srs.md)
- 架構：[`docs/architecture/system-architecture.md`](../../docs/architecture/system-architecture.md)
- 文件總索引：[`docs/README.md`](../../docs/README.md)
