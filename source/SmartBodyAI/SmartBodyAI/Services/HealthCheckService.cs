using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using System.Net;

namespace SmartBodyAI.Services;

public class HealthCheckService
{
    private const string DefaultSystemName = "SmartBodyAI Health Check";

    private readonly SettingService settingService;
    private readonly SmartAppSettingService smartAppSettingService;
    private readonly ConfigurationDiagnosticsService configurationDiagnosticsService;
    private readonly ISmartDiscoveryService smartDiscoveryService;
    private readonly IWebHostEnvironment environment;
    private readonly IConfiguration configuration;
    private readonly ILogger<HealthCheckService> logger;

    public HealthCheckService(
        SettingService settingService,
        SmartAppSettingService smartAppSettingService,
        ConfigurationDiagnosticsService configurationDiagnosticsService,
        ISmartDiscoveryService smartDiscoveryService,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<HealthCheckService> logger)
    {
        this.settingService = settingService;
        this.smartAppSettingService = smartAppSettingService;
        this.configurationDiagnosticsService = configurationDiagnosticsService;
        this.smartDiscoveryService = smartDiscoveryService;
        this.environment = environment;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<HealthCheckSummary> GenerateAsync(string? queryIss, string? queryLaunch, bool? queryDebug, CancellationToken cancellationToken = default)
    {
        SettingModel boundSetting = CloneSetting(settingService.GetValue());
        SmartAppSettingModel runtimeSetting = CloneRuntimeSetting(smartAppSettingService.Data);
        SystemInformationModel systemInformation = boundSetting.SystemInformation ?? new SystemInformationModel();

        string effectiveFhirServerUrl = string.IsNullOrWhiteSpace(queryIss) ? runtimeSetting.FhirServerUrl : queryIss!;
        string? effectiveLaunch = string.IsNullOrWhiteSpace(queryLaunch) ? runtimeSetting.Launch : queryLaunch;

        HealthCheckSummary summary = new()
        {
            CheckedAt = DateTimeOffset.Now,
            SystemName = string.IsNullOrWhiteSpace(systemInformation.SystemName) ? DefaultSystemName : systemInformation.SystemName,
            SystemVersion = systemInformation.SystemVersion ?? string.Empty,
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
            BuildDirectoryIndicator("upload-dicom-path", "Upload DICOM Path", boundSetting.UploadDicomPath),
            BuildDirectoryIndicator("upload-dicom-temp-path", "Upload DICOM Temp Path", boundSetting.UploadDicomTempPath),
            BuildDirectoryIndicator("dicom-image-path", "DICOM Image Path", boundSetting.DicomImagePath),
            BuildDirectoryIndicator("ai-result-path", "AI Result Path", boundSetting.AIResultPath),
            BuildSampleDataIndicator(),
            BuildDicomStaticPathIndicator(boundSetting.DicomImagePath)
        ];

        await Task.Delay(100, cancellationToken);
        ExternalFhirCheckResult fhirCheck = await CheckFhirAsync(effectiveFhirServerUrl, cancellationToken);
        summary.ResolvedAuthorizeUrl = fhirCheck.AuthorizeUrl;
        summary.ResolvedTokenUrl = fhirCheck.TokenUrl;
        summary.FhirMetadataSource = fhirCheck.MetadataSource;

        indicators.Add(fhirCheck.BaseConnectivityIndicator);
        indicators.Add(fhirCheck.SmartConfigurationIndicator);
        indicators.Add(fhirCheck.MetadataFallbackIndicator);
        indicators.Add(BuildOAuthEndpointIndicator(fhirCheck));
        indicators.Add(BuildStandaloneCapabilityIndicator(fhirCheck));
        indicators.Add(BuildOidcCapabilityIndicator(fhirCheck));
        indicators.Add(await BuildInferenceIndicator(boundSetting.InferenceHostApi, cancellationToken));
        indicators.Add(BuildAuthorizeUrlPrerequisiteIndicator(boundSetting, effectiveFhirServerUrl, summary.ResolvedAuthorizeUrl));
        indicators.Add(BuildBasicAuthIndicator(boundSetting));
        indicators.Add(BuildLaunchQueryConsistencyIndicator());

        summary.Indicators = indicators;
        summary.GreenCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Green);
        summary.YellowCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Yellow);
        summary.RedCount = indicators.Count(x => x.Status == HealthIndicatorStatus.Red);
        summary.TotalScore = indicators.Count == 0 ? 0 : (int)Math.Round(indicators.Average(x => x.Score), MidpointRounding.AwayFromZero);
        summary.Highlights = indicators
            .Where(x => x.Status != HealthIndicatorStatus.Green)
            .OrderBy(x => x.Status)
            .Select(x => $"{x.Title}: {x.Summary}")
            .ToList();

        if (summary.Highlights.Count == 0)
        {
            summary.Highlights.Add("All monitored indicators are green.");
        }

        return summary;
    }

    private HealthIndicatorResult BuildSectionBindingIndicator(SettingModel boundSetting)
    {
        bool sectionExists = configuration.GetSection(MagicObjectHelper.SmartAppSettingKey).Exists();
        bool looksBound = !string.IsNullOrWhiteSpace(boundSetting.FhirServerUrl) || !string.IsNullOrWhiteSpace(boundSetting.ClientId);
        return CreateIndicator(
            "smart-section-binding",
            "SmartAppSetting Binding",
            sectionExists && looksBound ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red,
            sectionExists && looksBound ? "SmartAppSetting section is bound successfully." : "SmartAppSetting section is missing or not bound.",
            $"Section exists: {sectionExists}. FhirServerUrl: {boundSetting.FhirServerUrl}. ClientId: {boundSetting.ClientId}.",
            "Ensure appsettings.json contains SmartAppSetting and that SettingModel is bound.");
    }

    private HealthIndicatorResult BuildRequiredFieldsIndicator(SettingModel boundSetting)
    {
        string[] missingFields = new[]
        {
            nameof(boundSetting.FhirServerUrl),
            nameof(boundSetting.RedirectUrl),
            nameof(boundSetting.ClientId),
            nameof(boundSetting.AuthorizationScope),
            nameof(boundSetting.InferenceHostApi),
            nameof(boundSetting.UploadDicomPath),
            nameof(boundSetting.UploadDicomTempPath),
            nameof(boundSetting.DicomImagePath),
            nameof(boundSetting.AIResultPath)
        }
        .Where(field => string.IsNullOrWhiteSpace(GetSettingValue(boundSetting, field)))
        .ToArray();

        return CreateIndicator(
            "required-fields",
            "Required SMART Settings",
            missingFields.Length == 0 ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red,
            missingFields.Length == 0 ? "All required settings are present." : $"Missing required settings: {string.Join(", ", missingFields)}.",
            "Checked fields: FhirServerUrl, RedirectUrl, ClientId, AuthorizationScope, InferenceHostApi, UploadDicomPath, UploadDicomTempPath, DicomImagePath, AIResultPath.",
            "Fill all required settings before running SMART launch.");
    }

    private HealthIndicatorResult BuildClientCredentialIndicator(SettingModel boundSetting)
    {
        if (string.IsNullOrWhiteSpace(boundSetting.ClientId))
        {
            return CreateIndicator("client-credentials", "Client Credentials", HealthIndicatorStatus.Red, "ClientId is missing.", "ClientId is empty.", "Set SmartAppSetting:ClientId.");
        }

        if (string.IsNullOrWhiteSpace(boundSetting.ClientSecret))
        {
            return CreateIndicator("client-credentials", "Client Credentials", HealthIndicatorStatus.Yellow, "ClientSecret is not loaded from configuration.", "This app targets confidential client + PKCE, so ClientSecret should come from environment variables or user secrets.", "Set SmartAppSetting__ClientSecret via environment variables or secrets storage.");
        }

        return CreateIndicator("client-credentials", "Client Credentials", HealthIndicatorStatus.Green, "ClientId and ClientSecret are available.", $"ClientId: {boundSetting.ClientId}. ClientSecret length: {boundSetting.ClientSecret.Length}.", "No action needed.");
    }

    private HealthIndicatorResult BuildEnvironmentOverrideIndicator(List<ConfigurationFieldDiagnostic> diagnostics)
    {
        ConfigurationFieldDiagnostic[] environmentBackedFields = diagnostics.Where(x => x.FromEnvironmentVariable).ToArray();
        if (environmentBackedFields.Length == 0)
        {
            return CreateIndicator("environment-override", "Environment Variable Overrides", HealthIndicatorStatus.Yellow, "No SmartAppSetting values are currently loaded from environment variables.", "Sensitive settings like ClientSecret should usually come from environment variables.", "Provide SmartAppSetting__ClientSecret and other sensitive settings from the environment.");
        }

        string labels = string.Join(", ", environmentBackedFields.Select(x => x.Label));
        return CreateIndicator("environment-override", "Environment Variable Overrides", HealthIndicatorStatus.Green, $"Environment-backed fields detected: {labels}.", string.Join(" ", environmentBackedFields.Select(x => $"{x.Label} <- {x.SourceProvider}")), "No action needed.");
    }

    private HealthIndicatorResult BuildIssOverrideIndicator(SmartAppSettingModel runtimeSetting, string? queryIss, string? queryLaunch)
    {
        if (string.IsNullOrWhiteSpace(queryIss))
        {
            return CreateIndicator("iss-override", "iss Override", HealthIndicatorStatus.Green, "No query iss override is active.", $"Runtime FhirServerUrl: {runtimeSetting.FhirServerUrl}. Launch: {queryLaunch}.", "No action needed.");
        }

        if (string.Equals(queryIss, runtimeSetting.FhirServerUrl, StringComparison.Ordinal))
        {
            return CreateIndicator("iss-override", "iss Override", HealthIndicatorStatus.Green, "Query iss matches the runtime FHIR server URL.", $"iss: {queryIss}. Runtime: {runtimeSetting.FhirServerUrl}.", "No action needed.");
        }

        return CreateIndicator("iss-override", "iss Override", HealthIndicatorStatus.Yellow, "Query iss overrides the runtime FHIR server URL.", $"iss: {queryIss}. Runtime: {runtimeSetting.FhirServerUrl}. launch: {queryLaunch}.", "This is expected for SMART launch entry points, but verify the correct issuer is being used.");
    }

    private HealthIndicatorResult BuildRedirectUrlIndicator(string redirectUrl)
    {
        bool isAbsolute = Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri? redirectUri)
            && (redirectUri.Scheme == Uri.UriSchemeHttp || redirectUri.Scheme == Uri.UriSchemeHttps);

        return CreateIndicator("redirect-url", "Redirect URL", isAbsolute ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, isAbsolute ? "Redirect URL is absolute." : "Redirect URL must be absolute.", $"RedirectUrl: {redirectUrl}.", "Configure an absolute http/https redirect URL and register it with the SMART authorization server.");
    }

    private HealthIndicatorResult BuildDirectoryIndicator(string key, string title, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CreateIndicator(key, title, HealthIndicatorStatus.Red, "Path is empty.", string.Empty, "Populate this path in configuration.");
        }

        bool exists = Directory.Exists(path);
        return CreateIndicator(key, title, exists ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, exists ? "Directory exists." : "Directory does not exist.", $"Path: {path}.", "Ensure the path exists or let Program.cs create it at startup.");
    }

    private HealthIndicatorResult BuildSampleDataIndicator()
    {
        string sample1 = Path.Combine(environment.ContentRootPath, "Datas", "sample1.png");
        string sample2 = Path.Combine(environment.ContentRootPath, "Datas", "sample2.png");
        bool sample1Exists = File.Exists(sample1);
        bool sample2Exists = File.Exists(sample2);

        return CreateIndicator("sample-data", "Sample Assets", sample1Exists && sample2Exists ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, sample1Exists && sample2Exists ? "Bundled sample assets are available." : "Bundled sample assets are missing.", $"sample1: {sample1Exists} ({sample1}), sample2: {sample2Exists} ({sample2}).", "Restore Datas/sample1.png and Datas/sample2.png.");
    }

    private HealthIndicatorResult BuildDicomStaticPathIndicator(string? dicomImagePath)
    {
        bool staticRequestPathConfigured = !string.IsNullOrWhiteSpace(MagicObjectHelper.DicomWebPath);
        bool imagePathConfigured = !string.IsNullOrWhiteSpace(dicomImagePath);
        return CreateIndicator("dicom-static-path", "DICOM Static Files", staticRequestPathConfigured && imagePathConfigured ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, staticRequestPathConfigured && imagePathConfigured ? "DICOM static file path is configured." : "DICOM static file path is incomplete.", $"RequestPath: {MagicObjectHelper.DicomWebPath}. DicomImagePath: {dicomImagePath}.", "Verify Program.cs maps the physical DICOM image path to the configured request path.");
    }

    private async Task<ExternalFhirCheckResult> CheckFhirAsync(string? effectiveFhirServerUrl, CancellationToken cancellationToken)
    {
        ExternalFhirCheckResult result = new();

        if (string.IsNullOrWhiteSpace(effectiveFhirServerUrl))
        {
            result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server Connectivity", HealthIndicatorStatus.Red, "FHIR server URL is missing.", string.Empty, "Configure FhirServerUrl or provide query iss.");
            result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", HealthIndicatorStatus.Red, "SMART discovery cannot start without a FHIR server URL.", string.Empty, "Configure the FHIR base URL.");
            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Red, "CapabilityStatement fallback cannot start without a FHIR server URL.", string.Empty, "Configure the FHIR base URL.");
            return result;
        }

        try
        {
            var discovery = await smartDiscoveryService.DiscoverAsync(effectiveFhirServerUrl, cancellationToken: cancellationToken);
            result.AuthorizeUrl = discovery.AuthorizeUrl;
            result.TokenUrl = discovery.TokenUrl;
            result.MetadataSource = discovery.MetadataSource;
            result.Capabilities = discovery.Capabilities;

            bool hasSource = !string.IsNullOrWhiteSpace(discovery.MetadataSource);
            result.BaseConnectivityIndicator = CreateIndicator(
                "fhir-base",
                "FHIR Server Connectivity",
                hasSource ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red,
                hasSource ? "SMART discovery reached the FHIR server." : "SMART discovery did not reach usable metadata.",
                $"Source: {discovery.MetadataSource}. Errors: {string.Join(" | ", discovery.ValidationErrors)}",
                "Verify the FHIR base URL, TLS configuration, and SMART metadata endpoints.");

            result.SmartConfigurationIndicator = CreateIndicator(
                "smart-configuration",
                "SMART Configuration",
                discovery.MetadataSource == ".well-known/smart-configuration" ? HealthIndicatorStatus.Green :
                hasSource ? HealthIndicatorStatus.Yellow : HealthIndicatorStatus.Red,
                discovery.MetadataSource == ".well-known/smart-configuration"
                    ? "SMART metadata resolved from /.well-known/smart-configuration."
                    : hasSource
                        ? "SMART metadata required fallback instead of well-known configuration."
                        : "SMART well-known configuration is unavailable.",
                $"AuthorizeUrl: {discovery.AuthorizeUrl}. TokenUrl: {discovery.TokenUrl}.",
                "Expose /.well-known/smart-configuration with complete SMART metadata when possible.");

            result.MetadataFallbackIndicator = CreateIndicator(
                "fhir-metadata",
                "FHIR Metadata Fallback",
                discovery.MetadataSource == "/metadata" ? HealthIndicatorStatus.Green :
                hasSource ? HealthIndicatorStatus.Yellow : HealthIndicatorStatus.Red,
                discovery.MetadataSource == "/metadata"
                    ? "CapabilityStatement fallback resolved SMART metadata."
                    : hasSource
                        ? "CapabilityStatement fallback was not required."
                        : "CapabilityStatement fallback did not produce usable SMART metadata.",
                $"Capabilities: {string.Join(", ", discovery.Capabilities)}. Errors: {string.Join(" | ", discovery.ValidationErrors)}",
                "Ensure /metadata exposes SMART oauth-uris and capabilities extensions.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMART discovery health check failed.");
            result.BaseConnectivityIndicator = CreateIndicator("fhir-base", "FHIR Server Connectivity", HealthIndicatorStatus.Red, "SMART discovery health check failed.", ex.Message, "Verify the FHIR base URL and network connectivity.");
            result.SmartConfigurationIndicator = CreateIndicator("smart-configuration", "SMART Configuration", HealthIndicatorStatus.Red, "SMART configuration lookup failed.", ex.Message, "Ensure /.well-known/smart-configuration is reachable.");
            result.MetadataFallbackIndicator = CreateIndicator("fhir-metadata", "FHIR Metadata Fallback", HealthIndicatorStatus.Red, "CapabilityStatement fallback failed.", ex.Message, "Ensure /metadata exposes SMART extensions.");
        }

        return result;
    }

    private HealthIndicatorResult BuildOAuthEndpointIndicator(ExternalFhirCheckResult fhirCheck)
    {
        bool authorizeValid = Uri.TryCreate(fhirCheck.AuthorizeUrl, UriKind.Absolute, out _);
        bool tokenValid = Uri.TryCreate(fhirCheck.TokenUrl, UriKind.Absolute, out _);
        HealthIndicatorStatus status = authorizeValid && tokenValid
            ? HealthIndicatorStatus.Green
            : fhirCheck.BaseConnectivityIndicator.Status == HealthIndicatorStatus.Red
                ? HealthIndicatorStatus.Red
                : HealthIndicatorStatus.Yellow;

        return CreateIndicator("oauth-endpoints", "Authorize / Token Endpoints", status, authorizeValid && tokenValid ? "Authorize and token endpoints are valid absolute URLs." : "Authorize or token endpoint is missing or invalid.", $"Source: {fhirCheck.MetadataSource}. AuthorizeUrl: {fhirCheck.AuthorizeUrl}. TokenUrl: {fhirCheck.TokenUrl}.", "Verify SMART metadata exposes both authorization_endpoint and token_endpoint.");
    }

    private HealthIndicatorResult BuildStandaloneCapabilityIndicator(ExternalFhirCheckResult fhirCheck)
    {
        string[] requiredCapabilities =
        [
            "launch-standalone",
            "context-standalone-patient",
            "permission-patient"
        ];

        string[] missing = requiredCapabilities.Where(capability => !fhirCheck.Capabilities.Contains(capability, StringComparer.Ordinal)).ToArray();
        return CreateIndicator(
            "smart-standalone-capabilities",
            "SMART Standalone Capabilities",
            missing.Length == 0 ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red,
            missing.Length == 0 ? "Required standalone capabilities are present." : $"Missing capabilities: {string.Join(", ", missing)}.",
            $"Capabilities: {string.Join(", ", fhirCheck.Capabilities)}",
            "Expose launch-standalone, context-standalone-patient, and permission-patient in SMART metadata.");
    }

    private HealthIndicatorResult BuildOidcCapabilityIndicator(ExternalFhirCheckResult fhirCheck)
    {
        bool hasOidc = fhirCheck.Capabilities.Contains("sso-openid-connect", StringComparer.Ordinal);
        return CreateIndicator(
            "smart-oidc-capability",
            "SMART OIDC Capability",
            hasOidc ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red,
            hasOidc ? "SMART metadata advertises OIDC support." : "SMART metadata is missing sso-openid-connect.",
            $"Capabilities: {string.Join(", ", fhirCheck.Capabilities)}",
            "Expose sso-openid-connect when id_token and fhirUser are required.");
    }

    private async Task<HealthIndicatorResult> BuildInferenceIndicator(string? inferenceHostApi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inferenceHostApi))
        {
            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Red, "InferenceHostApi is empty.", string.Empty, "Configure InferenceHostApi.");
        }

        using HttpClient httpClient = CreateHttpClient();

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(inferenceHostApi, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Green, "Inference host is reachable.", $"GET {inferenceHostApi} => {(int)response.StatusCode} {response.StatusCode}.", "No action needed.");
            }

            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Yellow, "Inference host responded but not with success.", $"GET {inferenceHostApi} => {(int)response.StatusCode} {response.StatusCode}.", "Prefer exposing a simple health endpoint for the inference service.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inference host check failed.");
            return CreateIndicator("inference-host", "Inference Host API", HealthIndicatorStatus.Red, "Inference host is unreachable.", ex.Message, "Verify the inference service URL, TLS, and network connectivity.");
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

        return CreateIndicator("authorize-url-prerequisites", "SMART Authorize URL Prerequisites", missing.Count == 0 ? HealthIndicatorStatus.Green : HealthIndicatorStatus.Red, missing.Count == 0 ? "All authorize URL prerequisites are present." : $"Missing prerequisites: {string.Join(", ", missing)}.", $"ClientId: {boundSetting.ClientId}. RedirectUrl: {boundSetting.RedirectUrl}. Scope length: {boundSetting.AuthorizationScope?.Length ?? 0}. Effective FHIR URL: {effectiveFhirServerUrl}. AuthorizeUrl: {resolvedAuthorizeUrl}.", "Provide all SMART launch prerequisites before redirecting to the authorization server.");
    }

    private HealthIndicatorResult BuildBasicAuthIndicator(SettingModel boundSetting)
    {
        if (string.IsNullOrWhiteSpace(boundSetting.ClientSecret))
        {
            return CreateIndicator("basic-auth-readiness", "Token Basic Auth Readiness", HealthIndicatorStatus.Yellow, "ClientSecret is not loaded.", "The app targets confidential client + PKCE, so ClientSecret should be provided securely at runtime.", "Load SmartAppSetting__ClientSecret from environment variables or secret storage.");
        }

        return CreateIndicator("basic-auth-readiness", "Token Basic Auth Readiness", HealthIndicatorStatus.Green, "ClientSecret is available for confidential client authentication.", $"ClientId: {boundSetting.ClientId}. ClientSecret length: {boundSetting.ClientSecret.Length}.", "No action needed.");
    }

    private HealthIndicatorResult BuildLaunchQueryConsistencyIndicator()
    {
        return CreateIndicator("launch-query-consistency", "Launch Query Consistency", HealthIndicatorStatus.Yellow, "Standalone mode is supported; EHR launch query forwarding is still not implemented.", "Home.razor forwards iss and debug, but not launch. This is acceptable for standalone-only support.", "Do not advertise EHR launch support until launch query propagation is implemented.");
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
            SystemInformation = CloneSystemInformation(setting.SystemInformation),
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

    private static SystemInformationModel CloneSystemInformation(SystemInformationModel? systemInformation)
    {
        if (systemInformation is null)
        {
            return new SystemInformationModel();
        }

        return new SystemInformationModel
        {
            SystemName = systemInformation.SystemName ?? string.Empty,
            SystemDescription = systemInformation.SystemDescription ?? string.Empty,
            SystemVersion = systemInformation.SystemVersion ?? string.Empty
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
        public List<string> Capabilities { get; set; } = [];
        public HealthIndicatorResult BaseConnectivityIndicator { get; set; } = new();
        public HealthIndicatorResult SmartConfigurationIndicator { get; set; } = new();
        public HealthIndicatorResult MetadataFallbackIndicator { get; set; } = new();
    }
}
