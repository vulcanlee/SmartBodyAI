# ADR 0005：SmartAgentApi 相依外部 CTMS 專案

- **狀態**：已採用（已知限制）

## 背景

後端 `SmartAgentApi` 的 AI 推論流程需重用既有 `CTMS` 系統的商業邏輯（影像處理、推論調度、風險評估模型等）。這些程式碼位於 repo 之外的 `CTMS` 原始碼樹。

## 決策

`SmartAgentApi.csproj` 直接以**相對路徑專案參考**外部 `CTMS` 專案：

- `..\..\..\..\CTMS\src\CTMS\AIAgent.Business\AIAgent.Business.csproj`
- `..\..\..\..\CTMS\src\CTMS\CTMS.Business\CTMS.Business.csproj`

## 後果

- **已知限制**：本機若沒有對應的 `CTMS` 原始碼，`SmartAgentApi` 無法還原或建置。
- 只需閱讀／調整前端 SMART 流程時，可單獨以 `SmartBodyAI` 為主。
- 要完整驗證 DICOM / AI 流程，須先備妥 `CTMS` 相依專案與執行環境。
- 修改後端建置設定時，須維持相依路徑正確，或在文件明確標註變更。

## 對應

- `source/SmartAgentApi/SmartAgentApi/SmartAgentApi.csproj`
- 約束：[`../../guidelines/constraints.md`](../../guidelines/constraints.md) C-10
- 亦見根目錄 `README.md`「已知限制」與 PRD [`../../prd/product-requirements.md`](../../prd/product-requirements.md)
