# 術語表（Glossary）

本表定義 SmartBodyAI 領域與技術術語，協助開發者與 AI 快速理解上下文。

| 術語 | 說明 |
|------|------|
| **SMART on FHIR** | 一套讓第三方應用安全存取 EHR/FHIR 資料的開放標準，結合 OAuth2、OIDC 與 FHIR。本專案為 SMART 病患應用（Patient App）。 |
| **FHIR（R4）** | HL7 制定的醫療資訊交換標準。本專案使用 R4 版，透過 `Hl7.Fhir.R4` 讀取 Patient、Observation、Encounter、MedicationRequest、ServiceRequest 等資源。 |
| **Standalone Launch** | 應用獨立啟動，自行向 FHIR 伺服器發起 SMART discovery 與授權（非由 EHR 帶入）。 |
| **EHR Launch** | 由 EHR 系統啟動應用，透過 `iss`（FHIR 伺服器位址）與 `launch`（啟動 token）參數帶入上下文。 |
| **SMART Discovery** | 探索授權端點與伺服器能力的程序：優先 `/.well-known/smart-configuration`，fallback `/metadata`（見 [ADR 0004](../architecture/adr/0004-discovery-fallback-metadata.md)）。 |
| **OAuth2 Authorization Code Flow** | 以授權碼換取 access token 的授權流程，本專案授權的基礎。 |
| **PKCE** | Proof Key for Code Exchange，以 `code_verifier`/`code_challenge` 防止授權碼攔截。本專案強制 `S256`（見 [ADR 0002](../architecture/adr/0002-pkce-s256-mandatory.md)）。 |
| **OIDC** | OpenID Connect，建立在 OAuth2 之上的身分驗證層，提供 `id_token`。本專案驗證 `fhirUser`、`sub`、`iss`。 |
| **Confidential Client** | 能安全保管機密（ClientSecret）的客戶端類型。本專案以 Blazor Server 伺服器端實作（見 [ADR 0001](../architecture/adr/0001-blazor-confidential-client.md)）。 |
| **Patient Context** | 授權後 token response 帶回的病患範圍（`patient` claim）。Standalone patient app 必須取得（見 [約束 C-8](../guidelines/constraints.md)）。 |
| **State** | OAuth 防 CSRF 的隨機字串，產生後保存並於 callback 比對；TTL 10 分鐘（見 [ADR 0003](../architecture/adr/0003-oauth-state-10min-ttl.md)）。 |
| **Scope** | 授權範圍字串（如 `launch/patient`、`patient/*.read`、`openid fhirUser profile`）。 |
| **DICOM** | 醫學影像與相關資訊的國際標準格式。本專案以 `fo-dicom` 處理；上傳為 ZIP。 |
| **AI 推論流程** | 後端 SmartAgentApi 透過外部 CTMS 邏輯執行的多階段（Phase1/2/3）影像分析，產出身體組成指標。 |
| **身體組成指標** | AI 結果指標：SMD（骨骼肌密度）、IMAT（肌間肌肉脂肪）、LAMA（低密度肌肉區域）、NAMA（正常密度肌肉區域）、SMA（骨骼肌面積）、SMI（骨骼肌指標）、Myosteatosis（肌肉脂肪變性）。 |
| **健康檢查（Readiness）** | `/health-check` 產生的系統就緒診斷，含多項綠／黃／紅燈指標與加權分數。 |
| **SMART Checklist** | `/smart-checklist` 人工審查清單，支援本機儲存與匯出／匯入。 |
| **九大透明性頁面** | `wwwroot/page9.html` 的應用透明性揭露靜態頁。 |
| **CTMS** | repo 外部的既有系統，後端 AI 推論重用其 `AIAgent.Business`、`CTMS.Business`（見 [ADR 0005](../architecture/adr/0005-external-ctms-dependency.md)）。 |
| **IsDebug / Debug 模式** | 除錯模式旗標，可由設定或查詢參數 `?debug=true` 控制，影響除錯訊息顯示。 |
| **workId / checkKey** | 後端上傳產生的工作識別碼；後續查詢與下載結果以此為鍵。 |
