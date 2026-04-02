using AntDesign;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using System.Text.Json;

namespace SmartBodyAI.Components.Views;

public partial class LaunchView
{
    [Parameter]
    public bool ShowMode { get; set; }
    [Parameter]
    public bool IsDebug { get; set; }
    [Parameter]
    public string? Iss { get; set; }
    //[Parameter]
    public string? LaunchCode { get; set; }

    [Inject]
    public INotificationService Notice { get; init; }
    [Inject]
    public NavigationManager NavigationManager { get; init; }
    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; init; }
    [Inject]
    public OAuthStateStoreService OAuthStateStoreService { get; init; }
    [Inject]
    public ILogger<LaunchView> logger { get; set; }

    public string ProcessingMessage { get; set; }
    string logMessage;

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        //return;
        if (firstRender)
        {
            try
            {
                logger.LogInformation($"LaunchView OnAfterRenderAsync: Iss={Iss}, Debug={IsDebug}");
                SmartAppSettingService.Data.IsDebug = IsDebug;
                if (string.IsNullOrEmpty(Iss) == false)
                {
                    SmartAppSettingService.Data.FhirServerUrl = Iss;
                }
                SmartAppSettingService.UpdateSetting(SmartAppSettingService.Data);
                var dataJson = JsonSerializer.Serialize(SmartAppSettingService.Data);
                logger.LogInformation($"LaunchView OnAfterRenderAsync: SmartAppSettingService.Data= {dataJson}");
                await System.Threading.Tasks.Task.Delay(500);

                _ = Notice.Open(new NotificationConfig()
                {
                    Message = "通知",
                    Key = Guid.NewGuid().ToString(),
                    Description = $"系統正在啟動與初始化中，請稍待...",
                    NotificationType = NotificationType.Info,
                    Duration = 5.0,
                });

                logMessage = "系統初始化中...";
                if (IsDebug)
                    await UpdateMessage(logMessage);
                KeepLaunchIss();
                logMessage = $"從 FHIR 伺服器取得 Metadata 資訊...";
                if (IsDebug)
                    await UpdateMessage(logMessage);
                var success = await GetMetadataAsync();
                var authUrl = string.Empty;

                if (success)
                {
                    var dataJson2 = JsonSerializer.Serialize(SmartAppSettingService.Data);
                    logMessage = $"成功從 FHIR 伺服器取得 Metadata 資訊，並解析出 OAuth 端點 URL。 {dataJson2}";
                    logger.LogInformation(logMessage);
                    logMessage = "正在處理授權請求，請稍候...";
                    if (IsDebug)
                        await UpdateMessage(logMessage);
                    authUrl = await GetAuthorizeUrlAsync();
                    logMessage = $"取得重新導向到授權伺服器 URL: {authUrl}";
                    if (IsDebug)
                        await UpdateMessage($"準備重新導向", logMessage, NotificationType.Warning, 3.0);
                }
                else
                {
                    logger.LogError("Failed to retrieve Metadata information from FHIR server.");
                    await UpdateMessage("發生例外異常", "從 FHIR 伺服器取得 Metadata 資訊失敗", NotificationType.Error, 3.0);
                }

                await System.Threading.Tasks.Task.Delay(500);
                if (IsDebug)
                    await UpdateMessage($"重新導向到授權伺服器 : {authUrl}");
                logger.LogInformation($"Redirecting to authorization server at: {authUrl}");
                await System.Threading.Tasks.Task.Delay(1000);
                NavigationManager.NavigateTo(authUrl);
            }
            catch (Exception ex)
            {
                await UpdateMessage($"發生例外異常", $"{ex.Message}", NotificationType.Error, 2.0);
            }
        }
    }

    async System.Threading.Tasks.Task UpdateMessage(string description)
    {
        await UpdateMessage("取得 OAuth2 授權碼", description, NotificationType.Info, 1);
    }

    async System.Threading.Tasks.Task UpdateMessage(string message, string description, NotificationType notificationType, double? duration)
    {
        if (notificationType == NotificationType.Error)
        {
            logger.LogError($"{message} - {description}");
        }
        else if (notificationType == NotificationType.Warning)
        {
            logger.LogWarning($"{message} - {description}");
        }
        else
        {
            logger.LogInformation($"{message} - {description}");
        }

        var task = Notice.Open(new NotificationConfig()
        {
            Message = message,
            Key = Guid.NewGuid().ToString(),
            Description = $"{description}",
            NotificationType = notificationType,
            Duration = duration,
        });
        await task;
    }

    /// <summary>
    /// 根據 ISS 是否有傳入值，決定要清空或保留 Smart App Setting Service 中的 Iss 及 Launch Code。也就是說要符合 Smart App 的 EHR Launch 或者 Standalone Launch 模式。
    /// </summary>
    public void KeepLaunchIss()
    {
        #region 若為 Standalone Launch 模式,則清空 Iss & Launch Code
        if (string.IsNullOrEmpty(Iss) || string.IsNullOrEmpty(LaunchCode))
        {
            SmartAppSettingService.Data.Iss = null;
            SmartAppSettingService.Data.Launch = null;
            return;
        }
        #endregion

        #region 若為 EHR Launch 模式,則會需要接收到 Iss & Launch Code，而 FHIR Server 的 URL 則是 Iss
        SmartAppSettingService.Data.Iss = Iss;
        SmartAppSettingService.Data.Launch = LaunchCode;
        SmartAppSettingService.Data.FhirServerUrl = Iss;
        #endregion
    }

    /// <summary>
    /// 從 FHIR 伺服器取得 Metadata 資訊,並解析 SMART on FHIR 所需的 OAuth 端點 URL。
    /// </summary>
    /// <returns>
    /// 如果成功取得 Authorize URL 和 Token URL 則返回 <c>true</c>;否則返回 <c>false</c>。
    /// </returns>
    /// <remarks>
    /// 此方法執行以下操作:
    /// <list type="number">
    /// <item>建立 FHIR 客戶端並向伺服器的 metadata 端點發送請求,取得 <see cref="CapabilityStatement"/>。</item>
    /// <item>遍歷 <see cref="CapabilityStatement.RestComponent"/> 中的安全性擴充 (Security Extension)。</item>
    /// <item>尋找符合 SMART on FHIR 標準的 OAuth URI 擴充 (http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris)。</item>
    /// <item>從擴充中提取 authorize 和 token 端點 URL,並儲存至 <see cref="SmartAppSettingService"/>。</item>
    /// <item>驗證必要的 URL 是否都已成功取得。</item>
    /// </list>
    /// 這是 SMART on FHIR 授權流程的第一步,必須在發起 OAuth 授權請求前完成。
    /// </remarks>
    /// <exception cref="Exception">
    /// 當無法連接到 FHIR 伺服器或解析 Metadata 時可能拋出例外。
    /// </exception>
    public async Task<bool> GetMetadataAsync()
    {
        string? fhirServerUrl = SmartAppSettingService.Data.FhirServerUrl?.TrimEnd('/');
        logger.LogInformation($"Connecting to FHIR server at: {fhirServerUrl}");

        SmartAppSettingService.Data.AuthorizeUrl = string.Empty;
        SmartAppSettingService.Data.TokenUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(fhirServerUrl))
        {
            logger.LogWarning("FHIR server URL is empty.");
            await UpdateMessage("FHIR server URL is empty.");
            return false;
        }

        try
        {
            if (await TryGetSmartConfigurationAsync(fhirServerUrl))
            {
                return true;
            }

            return await TryGetCapabilityStatementAsync(fhirServerUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to FHIR server to retrieve SMART metadata.");
            await UpdateMessage($"Error connecting to FHIR server: {ex.Message}");

            return false;
        }
    }

    async Task<bool> TryGetSmartConfigurationAsync(string fhirServerUrl)
    {
        string smartConfigurationUrl = $"{fhirServerUrl}/.well-known/smart-configuration";

        try
        {
            using HttpClient httpClient = new();
            using HttpResponseMessage response = await httpClient.GetAsync(smartConfigurationUrl);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to retrieve SMART configuration from {SmartConfigurationUrl}. StatusCode: {StatusCode}", smartConfigurationUrl, response.StatusCode);
                    await UpdateMessage($"Failed to retrieve SMART configuration from {smartConfigurationUrl} , Status code: {response.StatusCode}");

                return false;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using JsonDocument jsonDocument = await JsonDocument.ParseAsync(responseStream);

            if (jsonDocument.RootElement.TryGetProperty("authorization_endpoint", out JsonElement authorizationEndpoint))
            {
                SmartAppSettingService.Data.AuthorizeUrl = authorizationEndpoint.GetString() ?? string.Empty;
                logger.LogInformation("Successfully retrieved authorization endpoint from SMART configuration: {AuthorizeUrl}", SmartAppSettingService.Data.AuthorizeUrl);
            }
            else
            {
                logger.LogWarning("SMART configuration does not contain an authorization_endpoint.");
                await UpdateMessage($"SMART configuration does not contain an authorization_endpoint.");
            }

            if (jsonDocument.RootElement.TryGetProperty("token_endpoint", out JsonElement tokenEndpoint))
            {
                SmartAppSettingService.Data.TokenUrl = tokenEndpoint.GetString() ?? string.Empty;
                logger.LogInformation("Successfully retrieved token endpoint from SMART configuration: {TokenUrl}", SmartAppSettingService.Data.TokenUrl);
            }
            else
            {
                logger.LogWarning("SMART configuration does not contain a token_endpoint.");
                await UpdateMessage($"SMART configuration does not contain a token_endpoint.");
            }

            if (string.IsNullOrEmpty(SmartAppSettingService.Data.AuthorizeUrl) || string.IsNullOrEmpty(SmartAppSettingService.Data.TokenUrl))
            {
                logger.LogWarning("SMART configuration does not contain complete OAuth endpoints.");
                await UpdateMessage($"SMART configuration does not contain complete OAuth endpoints.");
                return false;
            }

            logger.LogInformation("Successfully retrieved SMART configuration from {SmartConfigurationUrl}", smartConfigurationUrl);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve SMART configuration from {SmartConfigurationUrl}", smartConfigurationUrl);
            await UpdateMessage($"Failed to retrieve SMART configuration from {smartConfigurationUrl} : {ex.Message}");
            return false;
        }
    }

    async Task<bool> TryGetCapabilityStatementAsync(string fhirServerUrl)
    {
        try
        {
            Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(fhirServerUrl);
            CapabilityStatement capabilities = (CapabilityStatement)(await fhirClient.GetAsync("metadata"));

            foreach (CapabilityStatement.RestComponent restComponent in capabilities.Rest)
            {
                if (restComponent.Security == null)
                {
                    continue;
                }

                foreach (Extension securityExt in restComponent.Security.Extension)
                {
                    if (securityExt.Url != "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris")
                    {
                        continue;
                    }

                    if ((securityExt.Extension == null) || (securityExt.Extension.Count == 0))
                    {
                        continue;
                    }

                    foreach (Extension smartExt in securityExt.Extension)
                    {
                        switch (smartExt.Url)
                        {
                            case "authorize":
                                SmartAppSettingService.Data.AuthorizeUrl = ((FhirUri)smartExt.Value).Value ?? string.Empty;
                                break;

                            case "token":
                                SmartAppSettingService.Data.TokenUrl = ((FhirUri)smartExt.Value).Value ?? string.Empty;
                                break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(SmartAppSettingService.Data.AuthorizeUrl) || string.IsNullOrEmpty(SmartAppSettingService.Data.TokenUrl))
            {
                logger.LogWarning("CapabilityStatement does not contain complete SMART OAuth endpoints.");
                var dataJson = JsonSerializer.Serialize(SmartAppSettingService.Data);
                await UpdateMessage($"CapabilityStatement does not contain complete SMART OAuth endpoints. Data : {dataJson}");
                return false;
            }

            logger.LogInformation("Successfully retrieved SMART OAuth endpoints from /metadata.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error connecting to FHIR server to retrieve CapabilityStatement metadata.");
            await UpdateMessage($"Error connecting to FHIR server to retrieve CapabilityStatement metadata: {ex.Message}");
            return false;
        }

        return true;
    }

    public async System.Threading.Tasks.Task<string> GetAuthorizeUrlAsync()
    {
        var state = Guid.NewGuid().ToString("N");
        SmartAppSettingService.Data.State = state;

        await OAuthStateStoreService.SaveAsync<SmartAppSettingModel>(state, SmartAppSettingService.Data, TimeSpan.FromMinutes(10));

        //Console.WriteLine($"Generated state: {SmartAppSettingService.Data.State}");

        // 建立 SMART on FHIR OAuth2 授權請求 URL，包含以下標準參數:
        // - response_type: 指定 OAuth2 流程類型為 "code" (授權碼流程)
        // - client_id: 此 SMART App 在 EHR 系統中註冊的唯一識別碼
        // - redirect_uri: 授權完成後，EHR 系統將用戶重新導向回此 App 的 URL
        // - scope: 請求存取的資源範圍 (例如: patient/*.read, launch/patient)
        // - state: 用於防止 CSRF 攻擊的隨機值，授權後會原封不動地返回
        // - launch: EHR Launch 模式下，由 EHR 系統提供的啟動上下文識別碼 (Standalone Launch 則為空)
        // - aud: 目標 FHIR 伺服器的 URL，用於指定要存取的資源伺服器
        string launchUrl = $"{SmartAppSettingService.Data.AuthorizeUrl}?response_type=code" +
            $"&client_id={SmartAppSettingService.Data.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(SmartAppSettingService.Data.RedirectUrl)}" +
            $"&scope={Uri.EscapeDataString(SmartAppSettingService.Data.AuthorizationScope)}" +
            $"&state={SmartAppSettingService.Data.State}" +
            $"&launch={SmartAppSettingService.Data.Launch}" +
            $"&aud={Uri.EscapeDataString(SmartAppSettingService.Data.FhirServerUrl)}";

        logger.LogInformation($"Generated SMART on FHIR authorization URL: {launchUrl}");
        return launchUrl;
    }
}
