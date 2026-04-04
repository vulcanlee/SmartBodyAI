下面是一份可直接拿去做設計說明與測試依據的文件草案，目標是讓你的 **ASP.NET Core Blazor（Server-side interactive）SMART App** 符合 **SMART App Launch: Launch and Authorization** 的核心要求，並能拿去對 **SMART sandbox / launcher** 做驗證。這份整理是以你提供的 SMART App Launch 規格頁為主，並補上 conformance、scope / launch context 與 sandbox 官方說明。([FHIR 建置網站][1])

---

# SMART on FHIR App 啟動與授權設計說明

**適用對象：** ASP.NET Core Blazor Server-side interactive 應用
**目標：** 通過 SMART sandbox 驗證，完成 Standalone Launch 與可擴充至 EHR Launch 的啟動授權流程
**主要依據：** SMART App Launch v2.2.0 CI Build 與 SMART Health IT 測試文件。SMART 規格頁本身標示為 continuous build，不是正式授權版出版物，因此實作時建議同時留意正式版本差異；但 sandbox 驗證仍會以實際 launcher / server 行為為準。([FHIR 建置網站][1])

## 1. 設計定位

你的系統如果是 **Blazor Server-side interactive**，而且 **client secret 只保存在伺服器端**，不暴露到瀏覽器，那它在 SMART 模型裡應被設計成 **confidential client**，不是 public client。SMART 規格明確說明，能保護 secret 的應用屬於 confidential app；不能保護 secret 的純前端瀏覽器 app 才是 public app。([FHIR 建置網站][1])

另外，SMART 要求 **所有 app 都必須支援 PKCE**，而且伺服器必須支援 `S256`，不得只用 `plain`。所以即使你是 confidential client，也不能因為有 client secret 就省略 PKCE。([FHIR 建置網站][1])

## 2. 你這個 Blazor App 建議採用的模式

### 建議模式

採用：

* **Authorization Code Flow**
* **Confidential Client**
* **PKCE (S256)**
* Token exchange 在 **伺服器端** 執行
* Access token / refresh token 儘量保存在 **server-side session / protected server storage**
* 前端瀏覽器只負責 redirect，不直接持有 client secret

這樣的原因是：
SMART 規格要求敏感資訊只能透過 TLS 傳輸、必須驗證 `state`、不得把 token 放在不安全的明文 cookie、且 confidential client 在 token endpoint 必須完成 client authentication。對 Blazor Server 來說，這正好適合把 OAuth 流程都放在後端完成。([FHIR 建置網站][1])

---

## 3. SMART App Launch 標準流程

SMART App Launch 的高階流程是：

1. App 註冊
2. 啟動 App（Standalone 或 EHR Launch）
3. 讀取 `/.well-known/smart-configuration`
4. 取得 authorization code
5. 以 code 換 access token
6. 呼叫 FHIR API
7. 視需要 refresh token ([FHIR 建置網站][1])

### 3.1 App 註冊

在註冊階段，SMART 要求至少要預先註冊：

* 一個或多個固定且完整的 `redirect_uri`
* 零個或多個固定且完整的 launch URL
* 若是 confidential client，還要建立 client authentication 機制

  * asymmetric：JWKS / JWKS URL
  * symmetric：client secret ([FHIR 建置網站][1])

對你的 Blazor App，至少應該先準備：

* `LaunchUrl`：例如 `/smart/launch`
* `RedirectUri`：例如 `/smart/callback`

而且這些 URL 要用 **固定、完整、可公開訪問的 HTTPS 網址**。

---

## 4. Standalone Launch 的實作方式

SMART 規格指出，Standalone Launch 本身沒有 EHR 先傳進來的 `launch` 參數；app 是從外部進入，再自己去做 discovery 與授權要求。([FHIR 建置網站][1])

### 4.1 啟動入口

你的 Blazor App 應有一個入口，例如：

`GET /smart/launch?iss={fhirBase}`

實務上 sandbox / launcher 常會幫你帶好 `iss`，或讓你在 launcher 頁面選擇 FHIR endpoint。SMART 的官方教學也明確提到，可以用 `launch.smarthealthit.org` 來模擬 EHR 啟動你的 app。([Smart Health IT][2])

### 4.2 Discovery

收到 `iss` 後，app 要以 FHIR base URL 為基礎，去抓：

`{iss}/.well-known/smart-configuration`

這是 SMART discovery 的標準做法。規格要求這個 endpoint 回傳 JSON，且內容應包含：

* `authorization_endpoint`
* `token_endpoint`
* `grant_types_supported`
* `capabilities`
* `code_challenge_methods_supported`

其中 `code_challenge_methods_supported` 必須含 `S256`。([FHIR 建置網站][1])

### 4.3 建立授權請求

接著你的 app 導向 `authorization_endpoint`，帶以下核心參數：

* `response_type=code`
* `client_id`
* `redirect_uri`
* `scope`
* `aud`
* `state`
* `code_challenge`
* `code_challenge_method=S256`

其中：

* `redirect_uri` 必須與註冊值完全匹配
* `state` 必須是每次請求都不可預測的新值，回來時必須驗證
* `aud` 在目前版本是 server 必須支援的參數
* PKCE 參數是必要的 ([FHIR 建置網站][1])

### 4.4 Scope 建議

若你的 app 是單病人視角 app，Standalone 常用 scope 組合可設計為：

`launch/patient patient/*.rs openid fhirUser offline_access`

說明如下：

* `launch/patient`：要求在 standalone 流程中建立 patient context
* `patient/*.rs`：要求病人範圍的 FHIR 讀/查權限
* `openid`：要求 OIDC 身分資訊
* `fhirUser`：要求目前登入使用者對應的 FHIR user 資訊
* `offline_access`：要求 refresh token，可在使用者離線後繼續換 token ([FHIR 建置網站][3])

注意：
若你要求 `patient/*...` 且授權成功，EHR 應建立 patient context；但有些伺服器也可能要求你明確帶 `launch/patient`，因此為了 sandbox 相容性，建議明確帶上。([FHIR 建置網站][3])

---

## 5. EHR Launch 的實作方式

若未來你也要支援 EHR Launch，流程上最大的差異是：EHR 啟動 app 時，會先帶入：

* `iss`
* `launch` ([FHIR 建置網站][1])

你的 app 之後在送授權請求時，必須：

* scope 包含 `launch`
* query parameter 再帶回同一個 `launch={launch id}`

這個 `launch` 是 opaque identifier，用來把 app 的授權請求和當前 EHR session 關聯起來。([FHIR 建置網站][1])

所以：

* **Standalone**：通常請 `launch/patient`
* **EHR Launch**：通常 scope 要有 `launch`，並回送 `launch` 參數；若想提示需要 patient / encounter context，也可再加 `launch/patient`、`launch/encounter` 當 hint。([FHIR 建置網站][1])

---

## 6. Token 交換設計

當授權伺服器把 browser redirect 回你的 `redirect_uri` 時，app 會收到：

* `code`
* `state` ([FHIR 建置網站][1])

此時你的後端必須先驗證：

* `state` 是否與原 session 綁定且完全相符

驗證通過後，再向 `token_endpoint` 送 `POST`，內容至少包含：

* `grant_type=authorization_code`
* `code`
* `redirect_uri`
* `code_verifier`

對 confidential client 而言，還要做 client authentication。若你採 **symmetric** 方式，SMART 規格寫得很清楚：要用 HTTP Basic Authentication，帳號是 `client_id`，密碼是 `client_secret`。([FHIR 建置網站][1])

### Token response 你要處理的欄位

SMART token response 可能包含：

* `access_token`
* `token_type`
* `expires_in`
* `scope`
* `id_token`
* `refresh_token`
* `patient`
* 其他 launch context，例如 `encounter` 等 ([FHIR 建置網站][1])

如果你有要求 `openid fhirUser`，則要能處理 `id_token`。SMART 規格也指出，若要求 `openid` 與 `fhirUser`，伺服器應能在 `id_token` 中提供 `fhirUser` claim。([FHIR 建置網站][3])

---

## 7. Blazor Server 的建議技術切分

### 7.1 建議路由

你可以這樣切：

* `/smart/launch`：啟動入口
* `/smart/callback`：接收 code 與 state
* `/app/...`：正式功能頁面

### 7.2 建議服務切分

* `SmartDiscoveryService`：抓 `/.well-known/smart-configuration`
* `SmartAuthorizeService`：產生 state、PKCE、組 authorization URL
* `SmartTokenService`：交換 token、refresh token
* `SmartSessionStore`：把 state、code_verifier、iss、token response 存在 server-side session
* `FhirApiClient`：用 access token 呼叫 FHIR API

### 7.3 不建議的做法

* 不要把 `client_secret` 放到 Blazor component 或 JS
* 不要把 `access_token` 明文放 cookie
* 不要把 callback 收到的參數原樣轉發到別的任意 URL
* 不要省略 `state`
* 不要不做 PKCE 就直接用 confidential client 流程 ([FHIR 建置網站][1])

---

## 8. Sandbox 驗證時最容易失敗的地方

根據 SMART 規格與 SMART Health IT 的 launcher 測試方式，最常見失敗點通常是：

### 8.1 `redirect_uri` 不完全匹配

SMART 規格要求 token exchange 時帶的 `redirect_uri`，必須與初始 authorization request 使用的值相同，且必須符合預註冊值。([FHIR 建置網站][1])

### 8.2 沒有做 PKCE 或用了錯的 method

SMART 明定 app 必須支援 PKCE，server 必須支援 `S256`，不可只依賴 `plain`。([FHIR 建置網站][1])

### 8.3 `state` 沒有驗證

這是規格明文要求。若 callback 不驗 state，就不算合規。([FHIR 建置網站][1])

### 8.4 沒先做 discovery 就硬寫死 authorize/token endpoint

SMART 明確要求 app 先從 `/.well-known/smart-configuration` 取得 SMART metadata。([FHIR 建置網站][1])

### 8.5 Scope 與 launch 類型不一致

* Standalone 應偏向 `launch/patient`
* EHR Launch 要回送 `launch` 參數，scope 也通常要帶 `launch` ([FHIR 建置網站][1])

### 8.6 對 confidential client 用錯 token endpoint 驗證方式

如果 sandbox 給的是 `client_secret` 模式，你要用 Basic Auth，而不是把 secret 隨便塞在 query string。([FHIR 建置網站][4])

---

# SMART App 查核 Checklist

下面這份 checklist 可以直接拿來做開發自查、測試案例、SIT/UAT 驗證。

## A. App 註冊檢查

* [ ] 已註冊固定、完整的 `redirect_uri`
* [ ] 已註冊固定、完整的 launch URL
* [ ] `client_id` 已正確取得
* [ ] 若為 confidential client，已配置 `client_secret` 或私鑰/JWKS
* [ ] App 對外網址為 HTTPS

## B. 啟動模式檢查

* [ ] Standalone Launch 可由外部網址成功進入
* [ ] EHR Launch 模式下可正確接收 `iss`
* [ ] EHR Launch 模式下可正確接收 `launch`
* [ ] Standalone 模式不依賴 EHR 預先給 `launch`

## C. Discovery 檢查

* [ ] App 會以 `iss + "/.well-known/smart-configuration"` 進行 discovery
* [ ] 能正確解析 `authorization_endpoint`
* [ ] 能正確解析 `token_endpoint`
* [ ] 能正確解析 `capabilities`
* [ ] 能正確解析 `code_challenge_methods_supported`
* [ ] 若 server 宣告 OIDC，能解析 `issuer` 與 `jwks_uri` ([FHIR 建置網站][5])

## D. Authorization Request 檢查

* [ ] `response_type=code`
* [ ] `client_id` 正確
* [ ] `redirect_uri` 與註冊值完全一致
* [ ] `state` 每次重新產生且不可預測
* [ ] `aud` 有帶 FHIR base URL
* [ ] `code_challenge` 已產生
* [ ] `code_challenge_method=S256`
* [ ] Standalone 時有請求 `launch/patient`（若需要 patient context）
* [ ] EHR Launch 時有帶 scope `launch`
* [ ] EHR Launch 時有把收到的 `launch` 值原樣回送 ([FHIR 建置網站][1])

## E. Callback 檢查

* [ ] 可接收 `code`
* [ ] 可接收 `state`
* [ ] 驗證 `state` 成功後才可往下
* [ ] `state` 驗證失敗時會拒絕流程
* [ ] 對 callback 參數有做基本安全驗證，不直接執行或轉址外部任意 URL ([FHIR 建置網站][1])

## F. Token Exchange 檢查

* [ ] 以 `POST` 呼叫 token endpoint
* [ ] Content-Type 為 `application/x-www-form-urlencoded`
* [ ] 帶 `grant_type=authorization_code`
* [ ] 帶 `code`
* [ ] 帶 `redirect_uri`
* [ ] 帶 `code_verifier`
* [ ] 若為 confidential-symmetric client，使用 HTTP Basic Auth 送 `client_id:client_secret`
* [ ] 不把 `client_secret` 暴露給瀏覽器 ([FHIR 建置網站][1])

## G. Token Response 檢查

* [ ] 成功解析 `access_token`
* [ ] 檢查 `token_type=Bearer`
* [ ] 讀取 `expires_in`
* [ ] 讀取實際授權後的 `scope`
* [ ] 若有 `id_token`，可驗證並解析
* [ ] 若要求 `openid fhirUser`，可處理 `fhirUser` claim
* [ ] 若有 `patient` / `encounter` launch context，可正確保存並使用
* [ ] 若有 `refresh_token`，可安全保存於 server-side storage ([FHIR 建置網站][1])

## H. FHIR API 呼叫檢查

* [ ] 呼叫 FHIR API 時會帶 `Authorization: Bearer {access_token}`
* [ ] 當取得 patient context 時，可正確讀取 `Patient/{id}`
* [ ] Scope 不足時能正確處理 401/403
* [ ] Token 過期時可依條件執行 refresh flow ([FHIR 建置網站][1])

## I. 安全檢查

* [ ] 全流程只走 TLS/HTTPS
* [ ] 不將 bearer token 存在明文 cookie
* [ ] 不把 callback 參數做 open redirect
* [ ] 不執行不可信輸入
* [ ] Token、state、code_verifier 都保存在 app 專屬伺服器端儲存區 ([FHIR 建置網站][1])

## J. Sandbox 驗證檢查

* [ ] 可由 SMART App Launcher 成功啟動 app
* [ ] Standalone 啟動可完成授權
* [ ] 需要 patient context 時，token response 可取得 `patient`
* [ ] 要求 `openid fhirUser` 時，可取得並解析身份資訊
* [ ] App 成功用 token 呼叫至少一個 FHIR resource
* [ ] 重新測試時不依賴舊 state / 舊 code
* [ ] 不同患者 / 不同啟動情境下可重複驗證成功 ([Smart Health IT][2])

---

# 建議的最小可通過版本

若你的目標是先「通過 sandbox 驗證」，建議先做最小可行版本：

1. 只先支援 **Standalone Launch**
2. 採 **confidential client + PKCE**
3. scope 先用
   `launch/patient patient/*.rs openid fhirUser`
4. 成功後畫面至少顯示：

   * 目前 patient id
   * 目前使用者 `fhirUser`
   * 讀取 `Patient` resource 的基本資料
5. 再補 `offline_access` 與 refresh token 流程

這樣最容易先打通 end-to-end。因為從規格來看，Standalone 核心重點就是 discovery、authorization code、token exchange、launch context 與 FHIR API access。([FHIR 建置網站][1])

---

# 針對 Blazor Server 的一句結論

對你的技術棧來說，最合理的 SMART 設計是：

**把 Blazor Server 當成 confidential SMART client，由伺服器端完成 discovery、PKCE、authorization code exchange、token 保存與 FHIR API 呼叫；前端只負責啟動與呈現。**

這樣最符合 SMART 規格，也最容易通過 sandbox 驗證。([FHIR 建置網站][1])

如果你要，我下一步可以直接幫你把這份文件再整理成 **正式的系統設計規格書格式**，或者直接補成 **ASP.NET Core Blazor 的流程圖 + Controller / Service 架構 + 範例程式骨架**。

[1]: https://build.fhir.org/ig/HL7/smart-app-launch/app-launch.html "App Launch: Launch and Authorization - SMART App Launch v2.2.0"
[2]: https://docs.smarthealthit.org/tutorials/javascript/?utm_source=chatgpt.com "Tutorial - Building a JavaScript App - SMART on FHIR"
[3]: https://build.fhir.org/ig/HL7/smart-app-launch/scopes-and-launch-context.html "App Launch: Scopes and Launch Context - SMART App Launch v2.2.0"
[4]: https://build.fhir.org/ig/HL7/smart-app-launch/client-confidential-symmetric.html "Client Authentication: Symmetric (shared secret) - SMART App Launch v2.2.0"
[5]: https://build.fhir.org/ig/HL7/smart-app-launch/conformance.html "Conformance - SMART App Launch v2.2.0"
