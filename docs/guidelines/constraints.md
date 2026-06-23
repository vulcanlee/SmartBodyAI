# 約束條件清單（Constraints）

> **本文件是所有開發工作的最高優先依據。** 新增需求或修正錯誤時，產出的做法**不得違反**以下任何一條約束。每條約束都附上「原因」與「對應程式碼／設定位置」，方便驗證。
>
> 若任務需求與本文件衝突，請先停止並向需求方確認，不要逕自繞過約束。

---

## C-1. ClientSecret 不得寫入版本控制

- **規則**：`SmartAppSetting:ClientSecret` 在版控的設定檔中必須留空，敏感值僅能透過**環境變數**或 **ASP.NET Core User Secrets** 注入。
- **原因**：避免機密外洩；正式環境機密與程式碼分離。
- **對應**：`source/SmartBodyAI/SmartBodyAI/appsettings.json`（`ClientSecret` 為空字串）、環境變數名稱 `SmartAppSetting__ClientId`、`SmartAppSetting__ClientSecret`。
- **延伸**：詳細設定方式見 [`../operations/production-environment-variable-sop.md`](../operations/production-environment-variable-sop.md)。機密也不得寫入 log。

## C-2. 版本號每次建置 Patch +1

- **規則**：每產生一個建置內容，`appsettings.json` 內 `SmartAppSetting:SystemInformation:SystemVersion` 的**最後一碼（Patch）加 1**（例：`1.2.36 → 1.2.37`，不進位、不分異動性質），並在 commit message 中說明版本變更。
- **原因**：可追溯每個建置版本；與 CHANGELOG、健康檢查頁顯示一致。
- **對應**：`CLAUDE.md`、`source/SmartBodyAI/SmartBodyAI/appsettings.json`。詳見 [`versioning.md`](versioning.md)。

## C-3. docs 下所有 .md 必須 UTF-8 含 BOM、無亂碼

- **規則**：`docs/` 下所有 `.md` 檔必須是 **UTF-8 含 BOM（位元組 0xEF 0xBB 0xBF）**，且不得含取代字元 U+FFFD（亂碼）。
- **原因**：確保 PowerShell 與各工具讀取繁體中文不出現亂碼；CI 強制把關。
- **對應**：檢驗腳本 `scripts/Test-DocsEncoding.ps1`（遞迴檢查，無 BOM 或含 U+FFFD 即失敗）。
- **驗證**：`pwsh scripts/Test-DocsEncoding.ps1`，所有檔案須回報 `OK BOM=True ReplacementCharacter=False`。

## C-4. 異動後須同步更新相關文件

- **規則**：每次程式碼異動後，須確認並更新受影響的文件（尤其 `reference/`、`architecture/`、`changelog/CHANGELOG.md`）。
- **原因**：避免文件與實作脫節，維持文件作為收斂依據的可信度。
- **對應**：`CLAUDE.md`。

## C-5. PKCE 強制使用 S256

- **規則**：所有 SMART 授權請求必須帶 `code_challenge`，且 `code_challenge_method` 固定為 `S256`，不得省略或改用 `plain`。
- **原因**：SMART on FHIR 安全要求；防止授權碼攔截攻擊。
- **對應**：`source/SmartBodyAI/SmartBodyAI/Models/SmartAppSettingModel.cs:8`（`DefaultCodeChallengeMethod = "S256"`）、`Helpers/PkceHelper`、`Services/SmartAuthorizationService.cs`。

## C-6. OAuth State 必須生成、保存、驗證、清理

- **規則**：授權流程須生成唯一 `state`，保存後於 callback 嚴格比對；不相符即視為失敗並拒絕；流程結束（成功或失敗）後須清除 state。State 存活時效為 **10 分鐘**。
- **原因**：防止 CSRF；限制重放時間窗。
- **對應**：`Services/SmartAuthorizationService.cs:52`（`TimeSpan.FromMinutes(10)`）與多處 `RemoveAsync`、`Servicers/OAuthStateStoreService.cs`。

## C-7. redirect_uri 必須與設定完全一致

- **規則**：授權請求送出的 `redirect_uri` 與授權伺服器登記值、`SmartAppSetting:RedirectUrl` 必須**完全一致**（含路徑、大小寫）。callback 目標頁固定為 `/patient-information`。
- **原因**：OAuth2 規範要求；不一致會導致授權失敗。
- **對應**：`source/SmartBodyAI/SmartBodyAI/appsettings.json`（`RedirectUrl`，預設 `https://localhost:7170/patient-information`）。

## C-8. Standalone patient app 必須驗證 patient context 與 token 類型

- **規則**：token 交換後須驗證 `token_type` 為 `Bearer`、`access_token` 存在；Standalone patient app 必須在 token response 取得 `patient` context；讀取到的 Patient 資源不可為 null。
- **原因**：確保後續 FHIR 呼叫有正確的病患上下文，避免空資料流入畫面。
- **對應**：`Services/SmartAuthorizationService.cs`（callback 與 token 驗證）、`Components/Views/PatientInformationView.razor.cs`。

## C-9. 外部 API 呼叫須記錄 Request URI

- **規則**：對 FHIR 伺服器、授權伺服器、推論服務等外部 API 的呼叫，須以 NLog 記錄 Request URI 與關鍵流程節點（避免記錄機密）。
- **原因**：可觀測性與除錯；健康檢查與問題追蹤仰賴日誌。
- **對應**：`nlog.config`、各 `Services/*`、`Servicers/*`。

## C-10. SmartAgentApi 對外部 CTMS 專案的相依

- **規則**：`SmartAgentApi` 參考 repo 外部的 `CTMS` 專案（`AIAgent.Business`、`CTMS.Business`）。修改後端建置設定時，須維持此相依關係的正確路徑，或在文件中明確標註變更。
- **原因**：缺少相依原始碼時 `SmartAgentApi` 無法還原／建置；此為已知限制。
- **對應**：`source/SmartAgentApi/SmartAgentApi/SmartAgentApi.csproj`、決策見 [`../architecture/adr/0005-external-ctms-dependency.md`](../architecture/adr/0005-external-ctms-dependency.md)。

---

## 變更約束本身的流程

以上約束若需新增、修改或廢止，須：
1. 在對應的 ADR（[`../architecture/adr/`](../architecture/adr/)）記錄決策與原因。
2. 同步更新本文件與受影響的 `reference/`、`architecture/` 文件。
3. 於 commit message 與 `changelog/CHANGELOG.md` 說明。
