# ADR 0001：以 Blazor Server 作為 Confidential Client

- **狀態**：已採用

## 背景

SmartBodyAI 需要實作 SMART on FHIR 授權流程。Client 類型可選 Public Client（純前端、無機密）或 Confidential Client（伺服器端持有機密）。本應用以 Blazor Server 實作，授權流程與 token 交換皆在伺服器端執行。

## 決策

採用 **Blazor Server 作為 Confidential Client**：

- token 交換在伺服器端進行，`ClientSecret`（若有）以 HTTP Basic Auth 帶入。
- `ClientSecret` 為可選；即使提供機密，仍**強制使用 PKCE**（見 [0002](0002-pkce-s256-mandatory.md)）。
- 機密不寫入版控，僅由環境變數或 User Secrets 注入（見 [`../../guidelines/constraints.md`](../../guidelines/constraints.md) C-1）。

## 後果

- 機密留在伺服器端，較 Public Client 安全。
- 需妥善管理正式環境機密注入（見 [`../../operations/production-environment-variable-sop.md`](../../operations/production-environment-variable-sop.md)）。
- 健康檢查在未提供 `ClientSecret` 時，相關指標以 Green 呈現（視為合法的 Public/無機密情境）。

## 對應

- `source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs`
- 設計背景：[`../SMART on FHIR App 啟動與授權設計說明.md`](../SMART%20on%20FHIR%20App%20啟動與授權設計說明.md)
