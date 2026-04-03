# SmartBodyAI SMART on FHIR Standalone Checklist

## Summary

- Scope: `standalone only`
- Supported profile: `SMART standalone patient app + confidential client + PKCE + OIDC`
- Not supported: `EHR launch`
- Sensitive setting note: `ClientSecret` must be provided from environment variables or secret storage, not committed to source control

## Checklist

| Checklist Item | Status | Implementation | Notes |
| --- | --- | --- | --- |
| Standalone entry point is available | 已符合 | [Home.razor](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/Home.razor), [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs) | App can start from `/` without an existing EHR session. |
| `iss` is accepted as launch input | 已符合 | [Home.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/Home.razor.cs), [LaunchPage.razor](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/LaunchPage.razor), [LaunchView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/LaunchView.razor.cs) | Query `iss` overrides runtime FHIR base URL for SMART launch. |
| SMART discovery prefers `/.well-known/smart-configuration` | 已符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs) | Authorization and token endpoints are resolved from well-known metadata when available. |
| SMART discovery falls back to `/metadata` | 已符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs) | CapabilityStatement fallback is used when well-known metadata is unavailable. |
| OAuth2 Authorization Code flow is used | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | Authorization requests always use `response_type=code`. |
| PKCE with `S256` is used | 已符合 | [PkceHelper.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Helpers/PkceHelper.cs), [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | `code_verifier`, `code_challenge`, and `code_challenge_method=S256` are generated and used. |
| `state` is generated and validated | 已符合 | [OAuthStateStoreService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Servicers/OAuthStateStoreService.cs), [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | Callback validation now depends on stored state and removes replayable state on failure or success. |
| Authorization request includes SMART-required parameters | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | Includes `client_id`, `redirect_uri`, `scope`, `state`, `aud`, PKCE parameters, and does not send `launch` for standalone mode. |
| Token exchange includes `code_verifier` | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | `authorization_code` exchange uses the stored PKCE verifier. |
| Standalone patient context is required | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [PatientInformationView.razor.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Views/PatientInformationView.razor.cs) | Token responses without `patient` are rejected. |
| OIDC `id_token` is processed | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs), [SmartResponse.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Models/SmartResponse.cs) | `id_token` is parsed and validated at the application level. |
| `fhirUser` claim is required for OIDC mode | 已符合 | [SmartAuthorizationService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs) | `id_token` must contain `fhirUser`, `sub`, and `iss`. |
| SMART standalone capabilities are verified | 已符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | `launch-standalone`, `context-standalone-patient`, and `permission-patient` are required. |
| OIDC capability is verified | 已符合 | [SmartDiscoveryService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/SmartDiscoveryService.cs), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | `sso-openid-connect` is required because this app now requires `id_token` and `fhirUser`. |
| Confidential client secret is kept out of committed config | 部分符合 | [appsettings.json](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/appsettings.json), [production-environment-variable-sop.md](/D:/Vulcan/GitHub/SmartBodyAI/docs/production-environment-variable-sop.md) | The committed config now leaves `ClientSecret` blank, but deployment must still load it securely at runtime. |
| Health check reports SMART readiness | 已符合 | [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs), [HealthCheckPage.razor](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/HealthCheckPage.razor) | Health check now reports discovery source, endpoint readiness, standalone capability readiness, and OIDC readiness. |
| EHR launch is supported | 不適用 | [Home.razor](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Components/Pages/Home.razor), [HealthCheckService.cs](/D:/Vulcan/GitHub/SmartBodyAI/source/SmartBodyAI/SmartBodyAI/Services/HealthCheckService.cs) | This implementation intentionally does not support EHR launch in this scope. |

## OIDC Support Notes

- The app requires `openid fhirUser profile` in the configured SMART scopes.
- `id_token` validation is application-level and currently checks JWT structure plus required claims.
- `fhirUser` is extracted from `id_token` and surfaced through SMART token validation before patient data loading starts.

## Runtime Configuration Notes

- `ClientSecret` is sensitive and must be injected through environment variables or User Secrets.
- Recommended runtime variables:
  - `SmartAppSetting__ClientSecret`
  - `SmartAppSetting__ClientId`
  - `SmartAppSetting__FhirServerUrl`

## Explicit Non-Goals

- This checklist does not certify EHR launch support.
- This checklist does not certify asymmetric client authentication.
- This checklist does not replace external SMART client registration with the target authorization server.
