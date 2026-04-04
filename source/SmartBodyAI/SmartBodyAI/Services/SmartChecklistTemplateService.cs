using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public class SmartChecklistTemplateService
{
    public SmartChecklistPageModel CreatePageModel()
    {
        SmartChecklistPageModel model = new()
        {
            Subtitle = "依據 SMART on FHIR App 啟動與授權設計說明，逐項人工評估 standalone patient app 是否具備通過 sandbox 的條件。",
            Sections =
            [
                CreateLaunchSection(),
                CreateDiscoverySection(),
                CreateAuthorizationSection(),
                CreateCallbackSection(),
                CreateTokenExchangeSection(),
                CreateTokenResponseSection(),
                CreateFhirApiSection(),
                CreateSecuritySection()
            ]
        };

        model.Recalculate();
        return model;
    }

    private static SmartChecklistSection CreateLaunchSection() => new()
    {
        Title = "啟動入口",
        Summary = "確認 app 是否具備正確的 standalone 啟動入口與必要輸入條件。",
        Items =
        [
            CreateRequiredItem(
                "launch-entry",
                "具備 standalone 啟動入口",
                "SMART sandbox 需要能直接啟動你的 app，若沒有固定入口，後續授權流程無法開始。",
                "應提供固定且可公開存取的 standalone 啟動 URL，例如 `/smart/launch`。",
                "確認 sandbox 或測試人員能直接開啟該 URL，並進入 SMART 流程準備畫面。",
                "建立獨立的 SMART 啟動路由，避免依賴首頁或其他功能頁手動進入。"),
            CreateRequiredItem(
                "launch-iss",
                "可接收並使用 iss",
                "Standalone launch 依賴 `iss` 指向 FHIR base URL，缺少它就無法做 discovery。",
                "啟動頁必須能接收 `iss`，並把它當成 SMART discovery 的輸入來源。",
                "以 `?iss=https://...` 開啟頁面，確認畫面與流程會使用該值。",
                "在啟動頁接收 query string 並在 UI 顯示目前使用的 FHIR server，避免誤判。"),
            CreateOptionalItem(
                "launch-registration",
                "已註冊固定 launch URL",
                "Sandbox 與授權伺服器通常要求事先登錄啟動 URL，避免流程被未知來源濫用。",
                "註冊的 launch URL 應與實際部署網址一致，且使用 HTTPS。",
                "比對平台設定與實際對外網址是否完全一致。",
                "重新整理 sandbox 註冊資料，避免使用過期的測試網址。")
        ]
    };

    private static SmartChecklistSection CreateDiscoverySection() => new()
    {
        Title = "SMART Discovery",
        Summary = "確認是否能找到正確的 SMART metadata 與 standalone 所需能力。",
        Items =
        [
            CreateRequiredItem(
                "well-known",
                "可讀取 /.well-known/smart-configuration",
                "SMART app 需先透過 discovery 找到授權與 token endpoint，不能只靠硬編碼。",
                "應使用 `{iss}/.well-known/smart-configuration` 取得 SMART metadata。",
                "測試是否能成功取得 JSON 並解析 SMART metadata。",
                "先補齊 well-known 讀取邏輯，必要時才以 CapabilityStatement 作為 fallback。"),
            CreateRequiredItem(
                "discovery-endpoints",
                "metadata 含 authorization_endpoint 與 token_endpoint",
                "沒有這兩個 endpoint，就無法建立授權與換 token 流程。",
                "SMART metadata 必須提供 `authorization_endpoint` 與 `token_endpoint`。",
                "確認解析結果不為空，且為有效絕對網址。",
                "修正 metadata 解析或檢查 server 是否正確公開這兩個欄位。"),
            CreateRequiredItem(
                "discovery-capabilities",
                "metadata 宣告 standalone 能力",
                "Sandbox 會以 capabilities 判斷是否支援 standalone patient app。",
                "至少應包含 `launch-standalone`、`context-standalone-patient`、`permission-patient`。",
                "檢查 capabilities 陣列是否包含必要值。",
                "補齊 SMART metadata 內的 capabilities 宣告。"),
            CreateOptionalItem(
                "discovery-oidc",
                "metadata 宣告 OIDC 能力",
                "若你要依賴 `id_token` 與 `fhirUser`，必須確認伺服器宣告支援 OIDC。",
                "應包含 `sso-openid-connect` 能力。",
                "確認 capabilities 內存在 OIDC 相關宣告。",
                "若不支援 OIDC，需調整 scope 與頁面預期；若要支援則補齊 capability。")
        ]
    };

    private static SmartChecklistSection CreateAuthorizationSection() => new()
    {
        Title = "Authorization Request",
        Summary = "確認送往授權伺服器的請求符合 SMART standalone 規範。",
        Items =
        [
            CreateRequiredItem(
                "authorize-code-flow",
                "使用 Authorization Code Flow",
                "SMART app 的標準流程是 authorization code，這是 sandbox 驗證的核心。",
                "授權請求應帶 `response_type=code`。",
                "檢查實際導向網址是否含 `response_type=code`。",
                "改用 authorization code flow，不要使用 implicit 或其他非預期流程。"),
            CreateRequiredItem(
                "authorize-client-redirect",
                "授權請求含 client_id 與 redirect_uri",
                "授權伺服器要用它們辨識 app，redirect_uri 不一致會直接失敗。",
                "授權請求必須帶正確 `client_id` 與與註冊完全一致的 `redirect_uri`。",
                "確認網址參數與 sandbox 註冊值逐字一致。",
                "統一程式設定與平台註冊值，避免環境間錯配。"),
            CreateRequiredItem(
                "authorize-state-aud",
                "授權請求含 state 與 aud",
                "`state` 用來防止 CSRF，`aud` 用來標記目標 FHIR server。",
                "授權請求必須帶不可預測的 `state` 與對應 `iss` 的 `aud`。",
                "檢查每次授權請求都會產生新 state，且 aud 指向正確 FHIR base URL。",
                "補 state 產生與儲存邏輯，並確保 aud 不是寫死錯誤網址。"),
            CreateRequiredItem(
                "authorize-pkce",
                "授權請求使用 PKCE S256",
                "SMART 規格要求支援 PKCE，這是 sandbox 常見必檢項目。",
                "應帶 `code_challenge` 與 `code_challenge_method=S256`。",
                "比對授權請求網址是否含對應 PKCE 參數。",
                "實作 PKCE 產生器，並固定使用 `S256`。"),
            CreateRequiredItem(
                "authorize-scope",
                "授權請求 scope 符合 standalone patient app",
                "沒有正確 scope，就拿不到 patient context、FHIR 權限或 OIDC 資訊。",
                "至少應涵蓋 `launch/patient`、`patient/*.read`、`openid`、`fhirUser`、`profile`。",
                "核對授權請求中的 scope 是否包含必要值。",
                "把 scope 統一收斂成可通過 sandbox 的最小集合。")
        ]
    };

    private static SmartChecklistSection CreateCallbackSection() => new()
    {
        Title = "Callback 驗證",
        Summary = "確認 redirect 回來後會正確驗證授權結果，而不是直接往下執行。",
        Items =
        [
            CreateRequiredItem(
                "callback-code-state",
                "callback 可接收 code 與 state",
                "若 callback 不能正確接收回傳參數，就無法完成 token exchange。",
                "redirect_uri 對應頁面必須能讀取 `code`、`state`，並處理 `error`。",
                "用 sandbox 完整跑一次，確認 callback 參數有被接收與顯示。",
                "在 callback 頁補 query parsing 與錯誤訊息呈現。"),
            CreateRequiredItem(
                "callback-state-validation",
                "callback 會驗證 state",
                "這是防止 CSRF 與流程遭竄改的必要條件，不可省略。",
                "只有在 callback 的 state 與啟動時保存的 state 一致時，流程才可繼續。",
                "模擬錯誤 state，確認系統會拒絕繼續處理。",
                "加入 state store 與失敗後中止流程的處理。"),
            CreateOptionalItem(
                "callback-error-handling",
                "callback 能處理授權失敗訊息",
                "若使用者拒絕授權或 server 拒絕請求，頁面要能說清楚原因。",
                "應能處理 `error` 與 `error_description`，並阻止後續 token exchange。",
                "模擬授權失敗時，確認頁面顯示可讀訊息。",
                "補齊錯誤顯示與回復建議，避免只看到空白頁。")
        ]
    };

    private static SmartChecklistSection CreateTokenExchangeSection() => new()
    {
        Title = "Token Exchange",
        Summary = "確認 app 能以 confidential client + PKCE 方式完成換 token。",
        Items =
        [
            CreateRequiredItem(
                "token-post",
                "token exchange 使用 POST 與 form-urlencoded",
                "token endpoint 對 HTTP 方法與內容格式很敏感，錯誤格式會直接失敗。",
                "應以 POST 並使用 `application/x-www-form-urlencoded` 送出 token 請求。",
                "檢查實際 request method 與 content type。",
                "改用標準 form body，避免 query string 或錯誤 JSON body。"),
            CreateRequiredItem(
                "token-auth-code-verifier",
                "token request 含 authorization_code 與 code_verifier",
                "PKCE 的安全性落點就在這一步，缺少 code_verifier 會被拒絕。",
                "應帶 `grant_type=authorization_code`、`code`、`redirect_uri`、`code_verifier`。",
                "在網路封包或紀錄中確認這些欄位都存在。",
                "從 state store 取回 code_verifier 並在換 token 時送出。"),
            CreateRequiredItem(
                "token-basic-auth",
                "confidential client 使用 Basic Auth",
                "若採 shared secret，token endpoint 通常要求 Basic Auth 識別 client。",
                "應以 `Authorization: Basic base64(client_id:client_secret)` 呼叫 token endpoint。",
                "確認 client secret 不會暴露到瀏覽器端，且 request header 正確。",
                "將 secret 留在 server 端並改用 Basic Auth header。")
        ]
    };

    private static SmartChecklistSection CreateTokenResponseSection() => new()
    {
        Title = "Token Response",
        Summary = "確認 sandbox 回來的 token payload 足以支撐 standalone patient app。",
        Items =
        [
            CreateRequiredItem(
                "token-bearer",
                "token response 含 Bearer access token",
                "FHIR API 呼叫依賴 Bearer access token，這是核心成功訊號。",
                "回應必須含 `access_token`，且 `token_type` 為 `Bearer`。",
                "確認 token response 內這兩個欄位存在且值正確。",
                "補 response parsing 與欄位驗證，對錯誤 token_type 直接擋下。"),
            CreateRequiredItem(
                "token-patient-context",
                "token response 含 patient context",
                "Standalone patient app 的主要價值就是能取得當前 patient context。",
                "回應應能提供 `patient`，供後續 FHIR 讀取使用。",
                "完成授權後確認回應中有 patient id。",
                "檢查 scope 是否包含 patient context 需求，並比對 sandbox 設定。"),
            CreateRequiredItem(
                "token-scope",
                "token response scope 覆蓋必要權限",
                "實際回傳 scope 代表最後核准結果，不能只看請求值。",
                "回應 scope 應至少含 patient read 與 OIDC 相關 scope。",
                "比對回傳 scope 是否仍保有 `launch/patient`、`patient/*.read`、`openid`、`fhirUser`、`profile`。",
                "調整 app 註冊或 scope 請求，並確認 sandbox 支援該範圍。"),
            CreateOptionalItem(
                "token-id-token",
                "token response 可解析 id_token 與 fhirUser",
                "若畫面或流程依賴登入使用者資訊，需能解析 `id_token` 內的 `fhirUser`。",
                "有 OIDC scope 時，應能接收並解析 `id_token`，至少讀出 `fhirUser`、`sub`、`iss`。",
                "檢查 `id_token` 是否為有效 JWT，且包含必要 claims。",
                "加入 JWT 解析與必要 claim 驗證，避免把無效 token 當成功。")
        ]
    };

    private static SmartChecklistSection CreateFhirApiSection() => new()
    {
        Title = "FHIR API 存取",
        Summary = "確認拿到 token 後，app 真的能用它讀取 sandbox 資源。",
        Items =
        [
            CreateRequiredItem(
                "fhir-bearer-call",
                "FHIR API 呼叫帶 Bearer token",
                "拿到 token 但沒有正確帶入 API request，實務上仍然等於流程未完成。",
                "呼叫 FHIR API 時應使用 `Authorization: Bearer {access_token}`。",
                "檢查實際 API request header。",
                "把 access token 注入 FHIR client，而不是只存在記憶體卻未使用。"),
            CreateRequiredItem(
                "fhir-patient-read",
                "可成功讀取 Patient 資源",
                "這是 sandbox readiness 最直接的成功證據之一。",
                "取得 patient context 後，應能讀取對應 `Patient/{id}` 或等效病人資料。",
                "用 sandbox patient id 呼叫一次 Patient 讀取並確認成功。",
                "從 token response 保存 patient id，並建立最小 FHIR 讀取流程。"),
            CreateOptionalItem(
                "fhir-error-handling",
                "能處理 401/403 與權限不足",
                "若權限不足時沒有清楚回饋，排錯會非常困難。",
                "遇到未授權或 scope 不足時，應顯示具體錯誤而不是直接失敗。",
                "模擬 scope 不足或過期 token，確認畫面可辨識失敗原因。",
                "加入 API 錯誤分類與可讀提示，必要時提示重新授權。")
        ]
    };

    private static SmartChecklistSection CreateSecuritySection() => new()
    {
        Title = "安全性與沙盒準備",
        Summary = "確認部署與設定方式不會在 sandbox 前就埋下致命風險。",
        Items =
        [
            CreateRequiredItem(
                "security-https",
                "對外網址與 redirect URI 使用 HTTPS",
                "SMART 與 OAuth 流程依賴 TLS 保護授權資訊與 token。",
                "對外可用網址、redirect URI 與 sandbox 註冊值都應使用 HTTPS。",
                "檢查部署網址與 redirect URI 設定。",
                "改用 HTTPS 測試環境並同步更新 sandbox 註冊值。"),
            CreateRequiredItem(
                "security-secret-storage",
                "ClientSecret 不暴露於前端",
                "Confidential client 的前提就是 secret 只能存在伺服器端。",
                "ClientSecret 應儲存在 server-side configuration，不可送到瀏覽器或硬編碼前端。",
                "檢查前端 HTML、JS 與網路封包是否完全看不到 secret。",
                "改由環境變數或伺服器端祕密管理機制提供 secret。"),
            CreateOptionalItem(
                "security-repeatability",
                "可重複執行 sandbox 測試而不依賴舊狀態",
                "若流程依賴舊 state、舊 code 或殘留 session，結果會不穩定。",
                "每次測試都應產生新的 state / code verifier，且舊資料不影響新流程。",
                "連續跑多次授權流程，確認不需手動清 cache 才能成功。",
                "在流程完成或失敗後清理暫存 state，避免舊資料污染新測試。")
        ]
    };

    private static SmartChecklistItem CreateRequiredItem(
        string key,
        string title,
        string whyItMatters,
        string rule,
        string testResult,
        string improvementSuggestion)
    {
        return CreateItem(key, title, true, whyItMatters, rule, testResult, improvementSuggestion);
    }

    private static SmartChecklistItem CreateOptionalItem(
        string key,
        string title,
        string whyItMatters,
        string rule,
        string testResult,
        string improvementSuggestion)
    {
        return CreateItem(key, title, false, whyItMatters, rule, testResult, improvementSuggestion);
    }

    private static SmartChecklistItem CreateItem(
        string key,
        string title,
        bool isRequired,
        string whyItMatters,
        string rule,
        string testResult,
        string improvementSuggestion)
    {
        return new SmartChecklistItem
        {
            Key = key,
            Title = title,
            IsRequired = isRequired,
            Weight = isRequired ? 2 : 1,
            Status = HealthIndicatorStatus.Yellow,
            WhyItMatters = whyItMatters,
            Rule = rule,
            TestResult = testResult,
            FailureReason = isRequired
                ? "尚未完成人工確認，必要項目仍可能阻擋 sandbox 測試。"
                : "尚未完成人工確認，建議補上結果避免遺漏風險。",
            ImprovementSuggestion = improvementSuggestion
        };
    }
}
