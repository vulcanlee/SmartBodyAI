# ADR 0002：PKCE 強制使用 S256

- **狀態**：已採用

## 背景

OAuth2 Authorization Code Flow 可能遭授權碼攔截攻擊。PKCE（Proof Key for Code Exchange）以 `code_verifier` / `code_challenge` 緩解此風險，`code_challenge_method` 可為 `plain` 或 `S256`。

## 決策

**所有授權請求一律使用 PKCE，且 `code_challenge_method` 固定為 `S256`**：

- `code_challenge_method` 預設常數定義於 `SmartAppSettingModel.DefaultCodeChallengeMethod = "S256"`。
- 即使為 Confidential Client（有 `ClientSecret`）也不省略 PKCE。
- 不使用 `plain`。

## 後果

- 符合 SMART on FHIR 安全建議，降低授權碼攔截風險。
- 授權伺服器必須支援 S256（Discovery 時應確認）。

## 對應

- `source/SmartBodyAI/SmartBodyAI/Models/SmartAppSettingModel.cs:8`（`DefaultCodeChallengeMethod = "S256"`）
- `source/SmartBodyAI/SmartBodyAI/Helpers/PkceHelper.cs`
- `source/SmartBodyAI/SmartBodyAI/Services/SmartAuthorizationService.cs`
- 約束：[`../../guidelines/constraints.md`](../../guidelines/constraints.md) C-5
