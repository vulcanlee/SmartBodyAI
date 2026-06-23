# SmartBodyAI SMART on FHIR app 規格 checklist（standalone + EHR launch）

## 摘要

- 專案範圍：`standalone + EHR launch`
- 支援輪廓：`SMART standalone / EHR launch patient app + confidential client + PKCE + OIDC`
- 不支援範圍：`完整嚴格 OIDC 簽章與信任鏈驗證（目前為應用層基本驗證）`
- 敏感設定說明：`ClientSecret` 不應寫死在版本控制內，必須由環境變數或秘密管理機制提供

## Checklist

| 項目 | 狀態 | 實作對應 | 說明 |
| --- | --- | --- | --- |
| App 可從 standalone 入口啟動 | 符合 | [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs) | 可直接從首頁啟動 SMART 授權流程，不依賴 EHR 內嵌上下文。 |
| 可接受 `iss` 作為啟動輸入 | 符合 | [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs) | `iss` 可覆寫執行時的 FHIR base URL。 |
| Standalone 流程不強制依賴 `launch` | 符合 | [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs) | standalone 啟動不把 `launch` 當必要條件；若帶入 `iss`/`launch` 則走 EHR launch。 |
| SMART discovery 優先使用 `/.well-known/smart-configuration` | 符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs) | 會先查 SMART well-known metadata。 |
| SMART discovery 可 fallback 到 `/metadata` | 符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs) | 當 well-known metadata 不可用時，會改讀 CapabilityStatement。 |
| 會解析 `authorization_endpoint` 與 `token_endpoint` | 符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs) | 會取得並驗證 authorize/token endpoint。 |
| 會驗證 standalone 所需 capability | 符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | 會檢查 `launch-standalone`、`context-standalone-patient`、`permission-patient`。 |
| 會驗證 OIDC capability | 符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | 目前設計要求 `sso-openid-connect`。 |
| 使用 OAuth2 Authorization Code flow | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 授權請求固定使用 `response_type=code`。 |
| 使用 PKCE 與 `S256` | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 會生成 `code_verifier`、`code_challenge` 與 `code_challenge_method=S256`。 |
| 使用 `state` 防止重放與 CSRF | 符合 | [OAuthStateStoreService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Servicers/OAuthStateStoreService.cs), [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 會儲存、載回、驗證 `state`，並於成功或失敗後移除。 |
| 授權請求包含 SMART 必要參數 | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 包含 `client_id`、`redirect_uri`、`scope`、`state`、`aud` 與 PKCE 參數。 |
| Callback 可接收 `code`、`state`、`error`、`error_description` | 符合 | [PatientInformationPage.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/PatientInformationPage.razor.cs) | callback query 參數已定義。 |
| Callback 後可從快取恢復 SMART 狀態 | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [PatientInformationView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/PatientInformationView.razor.cs) | 目前會回存並載回 `AuthorizeUrl`、`TokenUrl`、`ClientId`、`ClientSecret`、`RedirectUrl` 等授權必要欄位。 |
| Token exchange 使用 `authorization_code` | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 以授權碼向 token endpoint 換 token。 |
| Token exchange 會帶 `code_verifier` | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 會送出先前保存的 PKCE verifier。 |
| 支援 confidential client + PKCE | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [appsettings.json](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json) | 若有 `ClientSecret`，會用 Basic Auth 呼叫 token endpoint。 |
| `ClientSecret` 不再寫入 committed config | 部分符合 | [appsettings.json](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json), [production-environment-variable-sop.md](production-environment-variable-sop.md) | committed config 已留空，但正式部署仍必須安全地注入秘密值。 |
| Token response 會驗證 `token_type=Bearer` | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | `token_type` 不是 `Bearer` 會被拒絕。 |
| Token response 會驗證 `access_token` | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 缺少 `access_token` 會視為失敗。 |
| Token response 會驗證 patient context | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 缺少 `patient` 會被拒絕，符合 standalone patient app 的要求。 |
| Token response 會驗證必要 scopes | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [appsettings.json](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json) | 至少要求 `launch/patient` 與 `patient/*.read`，並搭配 OIDC scopes。 |
| 已要求 OIDC scopes | 符合 | [appsettings.json](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json) | 設定要求 `openid fhirUser profile`。 |
| `id_token` 已處理 | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [SmartResponse.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Models/SmartResponse.cs) | 會 parse JWT 並檢查必要 claims。 |
| `fhirUser` 已處理 | 符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [SmartResponse.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Models/SmartResponse.cs) | 會從 `id_token` 取出 `fhirUser`。 |
| 取得 token 後可用 Bearer 呼叫 FHIR API | 符合 | [PatientInformationView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/PatientInformationView.razor.cs) | 會用 Bearer token 存取 Patient、Observation 等資源。 |
| Health check 可顯示 SMART readiness | 符合 | [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | 會顯示 discovery 來源、endpoint readiness、standalone capability readiness 與 OIDC readiness。 |
| 支援 EHR launch | 符合 | [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | 由 Home.razor 與 `/launch` 接收 `iss`、`launch`、`debug` 並帶入 SMART 授權流程；健康檢查的 Launch Query Consistency 指標亦標示支援 standalone 與 EHR launch。 |
| 完整嚴格 OIDC 安全驗證 | 部分符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | 目前有 JWT 結構與 claim 驗證，但不是最完整的 OIDC 簽章與信任鏈驗證。 |

## OIDC 支援說明

- 本專案目前要求 `openid fhirUser profile`。
- `id_token` 的處理方式是應用層基本驗證，會檢查 JWT 可解析、並驗證 `fhirUser`、`sub`、`iss` 等必要 claims。
- `fhirUser` 會在 token 驗證階段被擷取，之後才進入病人資料查詢流程。

## 執行與部署注意事項

- `ClientSecret` 屬敏感資訊，應透過環境變數、User Secrets 或秘密管理機制提供。
- 建議至少提供以下執行時設定：
  - `SmartAppSetting__ClientSecret`
  - `SmartAppSetting__ClientId`
  - `SmartAppSetting__FhirServerUrl`
- 實際是否可成功完成 SMART 授權，仍取決於外部 authorization server 是否正確註冊 `redirect_uri` 與 client。

## 限制與總結

- 本專案同時支援 `standalone` 與 `EHR launch`（接收 `iss`/`launch` 參數帶入授權流程）。
- 以目前實作來看，可評為「高度符合 SMART on FHIR standalone / EHR launch app 規格」。
- 已具備 standalone 與 EHR launch 的主要規格要件；EHR launch 的實際相容性仍取決於 EHR 端正確帶入 `iss`/`launch` 並註冊 client。
- OIDC 目前屬基本應用層驗證，不是最嚴格的完整 OIDC 簽章與信任鏈驗證實作。
