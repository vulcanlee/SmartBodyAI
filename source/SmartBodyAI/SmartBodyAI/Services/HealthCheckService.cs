using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using System.Net;
using System.Text.Json;

namespace SmartBodyAI.Services;

public class HealthCheckService
{
    private readonly SettingService settingService;
    private readonly SmartAppSettingService smartAppSettingService;
    private readonly ConfigurationDiagnosticsService configurationDiagnosticsService;
    private readonly IWebHostEnvironment environment;
    private readonly IConfiguration configuration;
    private readonly ILogger<HealthCheckService> logger;

    public HealthCheckService(
        SettingService settingService,
        SmartAppSettingService smartAppSettingService,
        ConfigurationDiagnosticsService configurationDiagnosticsService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<HealthCheckService> logger)
    {
        this.settingService = settingService;
        this.smartAppSettingService = smartAppSettingService;
        this.configurationDiagnosticsService = configurationDiagnosticsService;
        this.environment = environment;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async System.Threading.Tasks.Task<HealthCheckSummary> GenerateAsync(string? queryIss, string? queryLaunch, bool? queryDebug, CancellationToken cancellationToken = default)
    {
        SettingModel boundSetting = CloneSetting(settingService.GetValue());
        SmartAppSettingModel runtimeSetting = CloneRuntimeSetting(smartAppSettingService.Data);

        string effectiveFhirServerUrl = string.IsNullOrWhiteSpace(queryIss) ? runtimeSetting.FhirServerUrl : queryIss!;
        string? effectiveLaunch = string.IsNullOrWhiteSpace(queryLaunch) ? runtimeSetting.Launch : queryLaunch;

        HealthCheckSummary summary = new()
        {
            CheckedAt = DateTimeOffset.Now,
            QueryIss = queryIss,
            QueryLaunch = queryLaunch,
            QueryDebug = queryDebug,
            BoundSetting = boundSetting,
            RuntimeSetting = runtimeSetting,
            EffectiveFhirServerUrl = effectiveFhirServerUrl,
            EffectiveLaunch = effectiveLaunch,
            IssOverridesRuntimeFhirServerUrl = !string.IsNullOrWhiteSpace(queryIss)
                && !string.Equals(queryIss, runtimeSetting.FhirServerUrl, StringComparison.Ordinal)
        };

        summary.ConfigurationFields = configurationDiagnosticsService.BuildDiagnostics(boundSetting, runtimeSetting, queryIss, queryLaunch, queryDebug);

        List<HealthIndicatorResult> indicators =
        [
            BuildSectionBindingIndicator(boundSetting),
            BuildRequiredFieldsIndicator(boundSetting),
            BuildClientCredentialIndicator(boundSetting),
            BuildEnvironmentOverrideIndicator(summary.ConfigurationFields),
            BuildIssOverrideIndicator(runtimeSetting, queryIss, queryLaunch),
            BuildRedirectUrlIndicator(boundSetting.RedirectUrl),
            BuildDirectoryIndicator("upload-dicom-path", "Upload DICOM 目錄", boundSetting.UploadDicomPath),
            BuildDirectoryIndicator("upload-dicom-temp-path", "Upload DICOM Temp 目錄", boundSetting.UploadDicomTempPath),
            BuildDirectoryIndicator("dicom-image-path", "DICOM Image 目錄", boundSetting.DicomImagePath),
            BuildDirectoryIndicator("ai-result-path", "AI Result 目錄", boundSetting.AIResultPath),
            BuildSampleDataIndicator(),
            BuildDicomStaticPathIndicator(boundSetting.DicomImagePath)
        ];

        ExternalFhirCheckResult fhirCheck = await CheckFhirAsync(effectiveFhirServerUrl, cancellationToken);
        summary.ResolvedAuthorizeUrl = fhirCheck.AuthorizeUrl;
        summary.ResolvedTokenUrl = fhirCheck.TokenUrl;
        summary.FhirMetadataSource = fhirCheck.MetadataSource;

        indicators.Add(fhirCheck.BaseConnectivityIndicator);
        indicators.Add(fhirCheck.SmartConfigurationIndicator);
        indicators.Add(fhirCheck.MetadataFallbackIndicator);
        indicators.Add(BuildOAuthEndpointIndicator(fhirCheck));
        indicators.Add(await BuildInferenceIndicator(boundSetting.InferenceHostApi, cancellationToken));
        indicators.Add(BuildAuthorizeUrlPrerequisiteIndicator(boundSetting, effectiveFhirServerUrl, summary.ResolvedAuthorizeUrl));
        indicators.Add(BuildBasicAuthIndicator(boundSetting));
        indicators.Add(BuildLaunchQueryConsistencyIndicator());

        summary.Indicators = indicators;
        summary.GreenCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Green);
        summary.YellowCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Yellow);
        summary.RedCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Red);
        summary.TotalScore = indicators.Count == 0 ? 0 : (int)Math.Round(indicators.Average(x => x.Score), MidpointRounding.AwayFromZero);
        summary.Highlights = indicators.Where(x => x.Status != HealthIndicatorStatus.Green).OrderBy(x => x.Status).Select(x => $"{x.Title}: {x.Summary}").ToList();
        if (summary.Highlights.Count == 0) summary.Highlights.Add("All monitored indicators are green.");

        return summary;
    }

    private HealthIndicatorResult BuildSectionBindingIndicator(SettingModel boundSetting)
    {
        bool sectionExists = configuration.GetSection(MagicObjectHelper.SmartAppSettingKey).Exists();
        bool looksBound = !string.IsNullOrWhiteSpace(boundSetting.FhirServerUrl) || !string.IsNullOrWhiteSpace(boundSetting.ClientId);
        return CreateIndicator("smart-section-binding", "SmartAppSetting 綁定", sectionExists && looksBound ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, sectionExists && looksBound ? "SmartAppSetting section 已成功綁定到 SettingModel。" : "SmartAppSetting section 缺失或綁定結果為空。", $"Section exists: {sectionExists}. FhirServerUrl: {boundSetting.FhirServerUrl}. ClientId: {boundSetting.ClientId}.", "確認 appsettings.json 或環境變數中存在 SmartAppSetting section 與必要欄位。");
    }

    private HealthIndicatorResult BuildRequiredFieldsIndicator(SettingModel boundSetting)
    {
        string[] missingFields = new[] { nameof(boundSetting.FhirServerUrl), nameof(boundSetting.RedirectUrl), nameof(boundSetting.ClientId), nameof(boundSetting.AuthorizationScope), nameof(boundSetting.InferenceHostApi), nameof(boundSetting.UploadDicomPath), nameof(boundSetting.UploadDicomTempPath), nameof(boundSetting.DicomImagePath), nameof(boundSetting.AIResultPath) }
            .Where(field => string.IsNullOrWhiteSpace(GetSettingValue(boundSetting, field)))
            .ToArray();

        return CreateIndicator("required-fields", "必要設定欄位", missingFields.Length == 0 ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, missingFields.Length == 0 ? "所有核心設定欄位都已提供。" : $"缺少必要欄位: {string.Join(", ", missingFields)}。", "Checked fields: FhirServerUrl, RedirectUrl, ClientId, AuthorizationScope, InferenceHostApi, UploadDicomPath, UploadDicomTempPath, DicomImagePath, AIResultPath.", "補齊缺失欄位後再重新載入 Health Check。");
    }

    private HealthIndicatorResult BuildClientCredentialIndicator(SettingModel boundSetting)
    {
        if (string.IsNullOrWhiteSpace(boundSetting.ClientId))
            return CreateIndicator("client-credentials", "Client Credentials 可讀性", HealthIndicatorStatus.Red, "ClientId 未讀到，SMART OAuth 流程無法成立。", "ClientId is empty.", "確認 SmartAppSetting:ClientId 是否存在於 appsettings 或環境變數。");

        if (string.IsNullOrWhiteSpace(boundSetting.ClientSecret))
            return CreateIndicator("client-credentials", "Client Credentials 可讀性", HealthIndicatorStatus.Yellow, "ClientId 已讀到，但 ClientSecret 為空。", "這在 public client 情境可接受，但若預期使用 confidential client 或由環境變數提供 secret，則目前未生效。", "若需要 confidential client，請設定 SmartAppSetting__ClientSecret 並重新檢查。");

        return CreateIndicator("client-credentials", "Client Credentials 可讀性", HealthIndicatorStatus.Green, "ClientId 與 ClientSecret 都已成功讀取。", $"ClientId: {boundSetting.ClientId}. ClientSecret length: {boundSetting.ClientSecret.Length}.", "無。");
    }

    private HealthIndicatorResult BuildEnvironmentOverrideIndicator(List<ConfigurationFieldDiagnostic> diagnostics)
    {
        ConfigurationFieldDiagnostic[] environmentBackedFields = diagnostics.Where(x => x.FromEnvironmentVariable).ToArray();
        if (environmentBackedFields.Length == 0)
            return CreateIndicator("environment-override", "Environment Variable 覆寫", HealthIndicatorStatus.Yellow, "目前沒有偵測到 SmartAppSetting 重要欄位由環境變數提供。", "系統仍可使用 appsettings.json 正常運作，但若部署策略預期秘密放在環境變數，這次檢查未看到覆寫痕跡。", "若要驗證環境變數覆寫，請設定 SmartAppSetting__ClientId / SmartAppSetting__ClientSecret 等 key。");

        string labels = string.Join(", ", environmentBackedFields.Select(x => x.Label));
        return CreateIndicator("environment-override", "Environment Variable 覆寫", HealthIndicatorStatus.Green, $"已偵測到環境變數覆寫生效: {labels}。", string.Join(" ", environmentBackedFields.Select(x => $"{x.Label} <- {x.SourceProvider}")), "無。");
    }

    private HealthIndicatorResult BuildIssOverrideIndicator(SmartAppSettingModel runtimeSetting, string? queryIss, string? queryLaunch)
    {
        if (string.IsNullOrWhiteSpace(queryIss))
            return CreateIndicator("iss-override", "iss 覆寫判讀", HealthIndicatorStatus.Green, "目前 request 沒有 `iss`，執行中 SMART 流程會使用現有 FhirServerUrl。", $"Runtime FhirServerUrl: {runtimeSetting.FhirServerUrl}.", "無。");

        if (string.Equals(queryIss, runtimeSetting.FhirServerUrl, StringComparison.Ordinal))
            return CreateIndicator("iss-override", "iss 覆寫判讀", HealthIndicatorStatus.Green, "`iss` 與目前執行中的 FhirServerUrl 一致。", $"iss: {queryIss}. Runtime: {runtimeSetting.FhirServerUrl}. launch: {queryLaunch}.", "無。");

        return CreateIndicator("iss-override", "iss 覆寫判讀", HealthIndicatorStatus.Yellow, "`iss` 會覆寫 SMART 流程使用的 FhirServerUrl，但不會改寫全域 IConfiguration。", $"iss: {queryIss}. Runtime FhirServerUrl: {runtimeSetting.FhirServerUrl}. launch: {queryLaunch}.", "若要驗證實際 SMART 啟動結果，請以相同 query 重新走 `/` 或 `/launch` 流程。");
    }

    private HealthIndicatorResult BuildRedirectUrlIndicator(string redirectUrl)
    {
        bool isAbsolute = Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri? redirectUri)
            && (redirectUri.Scheme == Uri.UriSchemeHttp || redirectUri.Scheme == Uri.UriSchemeHttps);

        return CreateIndicator("redirect-url", "RedirectUrl 格式", isAbsolute ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, isAbsolute ? "RedirectUrl 是合法 absolute URL。" : "RedirectUrl 不是合法的 absolute URL。", $"RedirectUrl: {redirectUrl}.", "請使用完整 http/https URL，並確保與 SMART client registration 一致。");
    }

    private HealthIndicatorResult BuildDirectoryIndicator(string key, string title, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return CreateIndicator(key, title, HealthIndicatorStatus.Red, "路徑設定為空。", string.Empty, "補上設定值並確認部署環境可存取該目錄。");

        bool exists = Directory.Exists(path);
        return CreateIndicator(key, title, exists ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, exists ? "目錄存在。" : "目錄不存在。", $"Path: {path}.", "確認目錄已建立，或讓應用程式啟動流程先建立對應資料夾。");
    }

    private HealthIndicatorResult BuildSampleDataIndicator()
    {
        string sample1 = Path.Combine(environment.ContentRootPath, "Datas", "sample1.png");
        string sample2 = Path.Combine(environment.ContentRootPath, "Datas", "sample2.png");
        bool sample1Exists = File.Exists(sample1);
        bool sample2Exists = File.Exists(sample2);

        return CreateIndicator("sample-data", "啟動用 Sample 圖檔", sample1Exists && sample2Exists ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, sample1Exists && sample2Exists ? "sample1.png 與 sample2.png 都存在。" : "Datas 資料夾缺少 sample1.png 或 sample2.png。", $"sample1: {sample1Exists} ({sample1}), sample2: {sample2Exists} ({sample2}).", "補回缺失的 sample 檔案，否則應用啟動時的 File.Copy 可能失敗。");
    }

    private HealthIndicatorResult BuildDicomStaticPathIndicator(string? dicomImagePath)
    {
        bool staticRequestPathConfigured = !string.IsNullOrWhiteSpace(MagicObjectHelper.DicomWebPath);
        bool imagePathConfigured = !string.IsNullOrWhiteSpace(dicomImagePath);
        return CreateIndicator("dicom-static-path", "DICOM 靜態檔案映射", staticRequestPathConfigured && imagePathConfigured ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, staticRequestPathConfigured && imagePathConfigured ? "DICOM Web request path 與實體目錄設定完整。" : "DICOM 靜態檔案設定不完整。", $"RequestPath: {MagicObjectHelper.DicomWebPath}. DicomImagePath: {dicomImagePath}.", "確認 Program.cs 的 StaticFileOptions 與 DicomImagePath 都有有效值。");
    }

    private async System.Threading.Tasks.Task<ExternalFhirCheckResult> CheckFhirAsync(string? effectiveFhirServerUrl, CancellationToken cancellationToken)
    {
        ExternalFhirCheckResult result = new();

        if (string.IsNullOrWhiteSpace(effectiveFhirServerUrl))
        {
            result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server 連線", HealthIndicatorStatus.Red, "FHIR Server URL 為空，無法進行外部檢查。", string.Empty, "提供 FhirServerUrl 或透過 query `iss` 指定目標。");
            result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", HealthIndicatorStatus.Red, "未提供 FHIR Server URL，無法檢查 `/.well-known/smart-configuration`。", string.Empty, "先補上有效的 FHIR Server URL。");
            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Red, "未提供 FHIR Server URL，無法檢查 `/metadata`。", string.Empty, "先補上有效的 FHIR Server URL。");
            return result;
        }

        string baseUrl = effectiveFhirServerUrl.TrimEnd('/');
        using HttpClient httpClient = CreateHttpClient();

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}/.well-known/smart-configuration", cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server 連線", HealthIndicatorStatus.Green, "FHIR Server 可連通。", $"GET {baseUrl}/.well-known/smart-configuration => {(int)response.StatusCode} {response.StatusCode}.", "無。");
                ParseSmartConfiguration(responseBody, result);
                result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", string.IsNullOrWhiteSpace(result.AuthorizeUrl) || string.IsNullOrWhiteSpace(result.TokenUrl) ? HealthIndicatorStatus.Yellow : HealthIndicatorStatus.Green, string.IsNullOrWhiteSpace(result.AuthorizeUrl) || string.IsNullOrWhiteSpace(result.TokenUrl) ? "SMART configuration 有回應，但缺少完整 OAuth endpoint。" : "SMART configuration 已成功回傳 OAuth endpoint。", $"AuthorizeUrl: {result.AuthorizeUrl}. TokenUrl: {result.TokenUrl}.", "確認 FHIR server 的 `.well-known/smart-configuration` 內容完整。");
                result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Green, "因為 SMART configuration 已成功取得，`/metadata` fallback 不需要啟用。", "Fallback skipped.", "無。");
                result.MetadataSource = ".well-known/smart-configuration";
                return result;
            }

            result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server 連線", HealthIndicatorStatus.Yellow, "FHIR Server 有回應，但 `/.well-known/smart-configuration` 未成功。", $"GET {baseUrl}/.well-known/smart-configuration => {(int)response.StatusCode} {response.StatusCode}.", "繼續檢查 `/metadata` fallback。");
            result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", HealthIndicatorStatus.Yellow, "`/.well-known/smart-configuration` 無法成功取得，將嘗試 `/metadata` fallback。", $"StatusCode: {(int)response.StatusCode}. Body length: {responseBody.Length}.", "確認伺服器是否支援 SMART configuration；若不支援，至少需確保 `/metadata` 有 SMART OAuth extension。");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reach FHIR SMART configuration endpoint.");
            result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server 連線", HealthIndicatorStatus.Red, "FHIR Server 無法連線。", ex.Message, "確認 FHIR Server URL、網路、TLS 憑證與防火牆設定。");
            result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", HealthIndicatorStatus.Red, "無法連線到 `/.well-known/smart-configuration`。", ex.Message, "先讓 FHIR Server 可連通。");
            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Red, "FHIR Server 連線失敗，因此 `/metadata` 也無法檢查。", ex.Message, "先讓 FHIR Server 可連通。");
            return result;
        }

        await TryGetMetadataFallbackAsync(baseUrl, result, cancellationToken);
        return result;
    }

    private async System.Threading.Tasks.Task TryGetMetadataFallbackAsync(string baseUrl, ExternalFhirCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            FhirClient client = new(baseUrl, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            CapabilityStatement capabilityStatement = (CapabilityStatement)await client.GetAsync("metadata", cancellationToken);
            bool foundOAuthUris = TryReadOAuthUrisFromCapabilityStatement(capabilityStatement, out string? authorizeUrl, out string? tokenUrl);
            if (!string.IsNullOrWhiteSpace(authorizeUrl)) result.AuthorizeUrl = authorizeUrl;
            if (!string.IsNullOrWhiteSpace(tokenUrl)) result.TokenUrl = tokenUrl;

            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", foundOAuthUris ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Yellow, foundOAuthUris ? "`/metadata` fallback 成功，已解析 SMART OAuth endpoint。" : "`/metadata` 有回應，但沒有完整 SMART OAuth extension。", $"AuthorizeUrl: {result.AuthorizeUrl}. TokenUrl: {result.TokenUrl}.", "若沒有 `.well-known/smart-configuration`，請確認 CapabilityStatement 的 oauth-uris extension 完整。");
            result.MetadataSource = foundOAuthUris ? "/metadata" : result.MetadataSource;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FHIR /metadata fallback failed.");
            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Red, "`/metadata` fallback 失敗。", ex.Message, "確認 FHIR Server 是否支援 `/metadata` 並可由目前網路環境存取。");
        }
    }

    private HealthIndicatorResult BuildOAuthEndpointIndicator(ExternalFhirCheckResult fhirCheck)
    {
        bool authorizeValid = Uri.TryCreate(fhirCheck.AuthorizeUrl, UriKind.Absolute, out _);
        bool tokenValid = Uri.TryCreate(fhirCheck.TokenUrl, UriKind.Absolute, out _);
        HealthIndicatorStatus status = authorizeValid && tokenValid ? HealthIndicatorStatus.Green : fhirCheck.BaseConnectivityIndicator.Status == HealthIndicatorStatus.Red ? HealthIndicatorStatus.Red : HealthIndicatorStatus.Yellow;
        return CreateIndicator("oauth-endpoints", "Authorize / Token URL 解析", status, authorizeValid && tokenValid ? "AuthorizeUrl 與 TokenUrl 都已成功解析。" : "尚未取得完整的 AuthorizeUrl / TokenUrl。", $"Source: {fhirCheck.MetadataSource}. AuthorizeUrl: {fhirCheck.AuthorizeUrl}. TokenUrl: {fhirCheck.TokenUrl}.", "檢查 SMART metadata 是否完整，或先走一次 `/launch` 流程讓 runtime 設定補齊。");
    }

    private async System.Threading.Tasks.Task<HealthIndicatorResult> BuildInferenceIndicator(string? inferenceHostApi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inferenceHostApi))
            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Red, "InferenceHostApi 為空。", string.Empty, "補上有效的 InferenceHostApi。");

        using HttpClient httpClient = CreateHttpClient();

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(inferenceHostApi, cancellationToken);
            if (response.IsSuccessStatusCode)
                return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Green, "Inference Host API 可連通且回傳成功狀態。", $"GET {inferenceHostApi} => {(int)response.StatusCode} {response.StatusCode}.", "無。");

            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Yellow, "Inference Host API 可連通，但根路徑未回傳成功狀態。", $"GET {inferenceHostApi} => {(int)response.StatusCode} {response.StatusCode}.", "若有專用 health endpoint，建議後續補上更精準的探測目標。");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inference host check failed.");
            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Red, "Inference Host API 無法連線。", ex.Message, "確認推論服務 URL、網路與 TLS 設定。");
        }
    }

    private HealthIndicatorResult BuildAuthorizeUrlPrerequisiteIndicator(SettingModel boundSetting, string effectiveFhirServerUrl, string? resolvedAuthorizeUrl)
    {
        List<string> missing = [];
        if (string.IsNullOrWhiteSpace(boundSetting.ClientId)) missing.Add("ClientId");
        if (string.IsNullOrWhiteSpace(boundSetting.RedirectUrl)) missing.Add("RedirectUrl");
        if (string.IsNullOrWhiteSpace(boundSetting.AuthorizationScope)) missing.Add("AuthorizationScope");
        if (string.IsNullOrWhiteSpace(effectiveFhirServerUrl)) missing.Add("Effective FhirServerUrl");
        if (string.IsNullOrWhiteSpace(resolvedAuthorizeUrl)) missing.Add("AuthorizeUrl");

        return CreateIndicator("authorize-url-prerequisites", "SMART Authorize URL 前置條件", missing.Count == 0 ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, missing.Count == 0 ? "組裝 SMART authorize URL 所需欄位已齊全。" : $"組裝 SMART authorize URL 缺少欄位: {string.Join(", ", missing)}。", $"ClientId: {boundSetting.ClientId}. RedirectUrl: {boundSetting.RedirectUrl}. Scope length: {boundSetting.AuthorizationScope?.Length ?? 0}. Effective FHIR URL: {effectiveFhirServerUrl}. AuthorizeUrl: {resolvedAuthorizeUrl}.", "先讓設定與 SMART metadata 完整，再進行授權流程。");
    }

    private HealthIndicatorResult BuildBasicAuthIndicator(SettingModel boundSetting)
    {
        if (string.IsNullOrWhiteSpace(boundSetting.ClientSecret))
            return CreateIndicator("basic-auth-readiness", "Token Basic Auth 準備度", HealthIndicatorStatus.Yellow, "ClientSecret 為空，token 交換時不會帶 Basic Authentication。", "這對 public client 可以接受，但 confidential client 會失敗。", "若此 SMART App 需要 confidential client，請設定 ClientSecret。");

        return CreateIndicator("basic-auth-readiness", "Token Basic Auth 準備度", HealthIndicatorStatus.Green, "ClientSecret 已提供，token 交換可以組出 Basic Authentication。", $"ClientId: {boundSetting.ClientId}. ClientSecret length: {boundSetting.ClientSecret.Length}.", "無。");
    }

    private HealthIndicatorResult BuildLaunchQueryConsistencyIndicator()
    {
        return CreateIndicator("launch-query-consistency", "首頁與 Launch 頁 query 一致性", HealthIndicatorStatus.Yellow, "目前首頁雖然接收 `launch` query，但沒有把它傳進 `LaunchView`。", "Health Check 根據程式碼靜態分析得知：Home.razor 只傳遞 Iss 與 IsDebug，LaunchCode 參數未被帶入。", "若要支援完整 EHR Launch，後續可補上首頁到 LaunchView 的 `LaunchCode` 傳遞。");
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClientHandler handler = new()
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static void ParseSmartConfiguration(string responseBody, ExternalFhirCheckResult result)
    {
        using JsonDocument document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("authorization_endpoint", out JsonElement authorizationEndpoint))
            result.AuthorizeUrl = authorizationEndpoint.GetString();
        if (document.RootElement.TryGetProperty("token_endpoint", out JsonElement tokenEndpoint))
            result.TokenUrl = tokenEndpoint.GetString();
    }

    private static bool TryReadOAuthUrisFromCapabilityStatement(CapabilityStatement capabilityStatement, out string? authorizeUrl, out string? tokenUrl)
    {
        authorizeUrl = null;
        tokenUrl = null;

        foreach (CapabilityStatement.RestComponent restComponent in capabilityStatement.Rest)
        {
            if (restComponent.Security?.Extension == null) continue;

            foreach (Extension securityExtension in restComponent.Security.Extension)
            {
                if (!string.Equals(securityExtension.Url, "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris", StringComparison.Ordinal))
                    continue;

                foreach (Extension smartExtension in securityExtension.Extension)
                {
                    switch (smartExtension.Url)
                    {
                        case "authorize":
                            authorizeUrl = (smartExtension.Value as FhirUri)?.Value;
                            break;
                        case "token":
                            tokenUrl = (smartExtension.Value as FhirUri)?.Value;
                            break;
                    }
                }
            }
        }

        return !string.IsNullOrWhiteSpace(authorizeUrl) && !string.IsNullOrWhiteSpace(tokenUrl);
    }

    private static HealthIndicatorResult CreateIndicator(string key, string title, HealthIndicatorStatus status, string summary, string details, string recommendation)
    {
        return new HealthIndicatorResult
        {
            Key = key,
            Title = title,
            Status = status,
            Summary = summary,
            Details = details,
            Recommendation = recommendation
        };
    }

    private static SettingModel CloneSetting(SettingModel setting)
    {
        return new SettingModel
        {
            FhirServerUrl = setting.FhirServerUrl,
            RedirectUrl = setting.RedirectUrl,
            ClientId = setting.ClientId,
            ClientSecret = setting.ClientSecret,
            InferenceHostApi = setting.InferenceHostApi,
            AuthorizationScope = setting.AuthorizationScope,
            ProcessDelayTimeInMilliSeconds = setting.ProcessDelayTimeInMilliSeconds,
            UploadDicomPath = setting.UploadDicomPath,
            UploadDicomTempPath = setting.UploadDicomTempPath,
            DicomImagePath = setting.DicomImagePath,
            AIResultPath = setting.AIResultPath,
            IsDebug = setting.IsDebug
        };
    }

    private static SmartAppSettingModel CloneRuntimeSetting(SmartAppSettingModel setting)
    {
        return new SmartAppSettingModel
        {
            AuthorizationScope = setting.AuthorizationScope,
            ProcessDelayTimeInMilliSeconds = setting.ProcessDelayTimeInMilliSeconds,
            FhirServerUrl = setting.FhirServerUrl,
            InferenceHostApi = setting.InferenceHostApi,
            AIResultPath = setting.AIResultPath,
            ClientId = setting.ClientId,
            ClientSecret = setting.ClientSecret,
            RedirectUrl = setting.RedirectUrl,
            AuthCode = setting.AuthCode,
            CodeVerifier = setting.CodeVerifier,
            CodeChallengeMethod = setting.CodeChallengeMethod,
            AuthorizationError = setting.AuthorizationError,
            AuthorizationErrorDescription = setting.AuthorizationErrorDescription,
            ClientState = setting.ClientState,
            TokenUrl = setting.TokenUrl,
            RefreshTokenUrl = setting.RefreshTokenUrl,
            AuthorizeUrl = setting.AuthorizeUrl,
            Iss = setting.Iss,
            Launch = setting.Launch,
            State = setting.State,
            IsDebug = setting.IsDebug
        };
    }

    private static string? GetSettingValue(SettingModel setting, string fieldName)
    {
        return fieldName switch
        {
            nameof(SettingModel.FhirServerUrl) => setting.FhirServerUrl,
            nameof(SettingModel.RedirectUrl) => setting.RedirectUrl,
            nameof(SettingModel.ClientId) => setting.ClientId,
            nameof(SettingModel.AuthorizationScope) => setting.AuthorizationScope,
            nameof(SettingModel.InferenceHostApi) => setting.InferenceHostApi,
            nameof(SettingModel.UploadDicomPath) => setting.UploadDicomPath,
            nameof(SettingModel.UploadDicomTempPath) => setting.UploadDicomTempPath,
            nameof(SettingModel.DicomImagePath) => setting.DicomImagePath,
            nameof(SettingModel.AIResultPath) => setting.AIResultPath,
            _ => null
        };
    }

    private sealed class ExternalFhirCheckResult
    {
        public string? AuthorizeUrl { get; set; }
        public string? TokenUrl { get; set; }
        public string? MetadataSource { get; set; }
        public HealthIndicatorResult BaseConnectivityIndicator { get; set; } = new();
        public HealthIndicatorResult SmartConfigurationIndicator { get; set; } = new();
        public HealthIndicatorResult MetadataFallbackIndicator { get; set; } = new();
    }
}
