# 架構決策紀錄（ADR）

本目錄記錄 SmartBodyAI 的重要架構決策。每份 ADR 採輕量格式：**狀態 / 背景 / 決策 / 後果**，並連結對應原始碼。

> 修改既有設計前，請先讀相關 ADR，理解當初決策的原因與取捨，避免誤改。新增或推翻決策時，新增一份 ADR 並更新本索引。

## 索引

| 編號 | 標題 | 狀態 |
|------|------|------|
| [0001](0001-blazor-confidential-client.md) | 以 Blazor Server 作為 Confidential Client | 已採用 |
| [0002](0002-pkce-s256-mandatory.md) | PKCE 強制使用 S256 | 已採用 |
| [0003](0003-oauth-state-10min-ttl.md) | OAuth State 採 10 分鐘 TTL | 已採用 |
| [0004](0004-discovery-fallback-metadata.md) | Discovery 先 well-known 後 metadata fallback | 已採用 |
| [0005](0005-external-ctms-dependency.md) | SmartAgentApi 相依外部 CTMS 專案 | 已採用（已知限制） |
