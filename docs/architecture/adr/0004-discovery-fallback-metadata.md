# ADR 0004：Discovery 先 well-known 後 metadata fallback

- **狀態**：已採用

## 背景

SMART on FHIR 應用啟動時需取得授權端點（authorization / token endpoint）與伺服器能力（capabilities）。資訊可能來自兩處：

- `/.well-known/smart-configuration`（SMART 標準探索文件）
- `/metadata`（FHIR CapabilityStatement，含 `oauth-uris` 與 `capabilities` extension）

不同 FHIR 伺服器對兩者的支援程度不一。

## 決策

`SmartDiscoveryService` 採**兩段式探索**：

1. **優先**讀取 `{fhirServerUrl}/.well-known/smart-configuration`。
2. 失敗時 **fallback** 至 `{fhirServerUrl}/metadata`，從 CapabilityStatement 的
   `oauth-uris` extension 取得端點、`capabilities` extension 取得能力。
3. 驗證 Standalone 必要能力（`launch-standalone`、`context-standalone-patient`、
   `permission-patient`、`sso-openid-connect`）與 OIDC 支援。

## 後果

- 對只支援其中一種探索方式的伺服器都能相容。
- 兩條路徑都需維護解析邏輯。

## 對應

- `source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs`
  - extension URL 常數：`oauth-uris`、`capabilities`
  - `RequiredStandaloneCapabilities`
- 健康檢查對應指標：SMART configuration / metadata fallback / OAuth endpoint / Standalone capability / OIDC capability
