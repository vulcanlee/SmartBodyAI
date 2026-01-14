using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Components;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;

namespace SmartBodyAI.Components.Views;

public partial class LaunchView
{
    //[Parameter]
    public string? Iss { get; set; }
    //[Parameter]
    public string? LaunchCode { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; init; }
    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; init; }
    [Inject]
    public OAuthStateStoreService OAuthStateStoreService { get; init; }

    public string ProcessingMessage { get; set; }

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {

                await UpdateMessage("系統初始化中...");
                KeepLaunchIss();
                await UpdateMessage("從 FHIR 伺服器取得 Metadata 資訊...");
                var success = await GetMetadataAsync();
                var authUrl = string.Empty;
                if (success)
                {
                    await UpdateMessage("正在處理授權請求，請稍候...");
                    authUrl = await GetAuthorizeUrlAsync();
                    await UpdateMessage($"取得重新導向到授權伺服器 URL:{authUrl}");
                }
                else
                {
                    await UpdateMessage("從 FHIR 伺服器取得 Metadata 資訊失敗");
                }

                await UpdateMessage($"重新導向到授權伺服器");
                NavigationManager.NavigateTo(authUrl);
            }
            catch (Exception ex)
            {
                await UpdateMessage($"發生例外異常 : {ex.Message}");
            }
        }
    }

    async System.Threading.Tasks.Task UpdateMessage(string message)
    {
        ProcessingMessage = message;
        await System.Threading.Tasks.Task.Delay(1000);
        StateHasChanged();
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
        Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(SmartAppSettingService.Data.FhirServerUrl);

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
                            SmartAppSettingService.Data.AuthorizeUrl = ((FhirUri)smartExt.Value).Value.ToString();
                            break;

                        case "token":
                            SmartAppSettingService.Data.TokenUrl = ((FhirUri)smartExt.Value).Value.ToString();
                            break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(SmartAppSettingService.Data.AuthorizeUrl) || string.IsNullOrEmpty(SmartAppSettingService.Data.TokenUrl))
        {
            return false;
        }

        return true;
    }

    public async System.Threading.Tasks.Task<string> GetAuthorizeUrlAsync()
    {
        var state = Guid.NewGuid().ToString("N");
        SmartAppSettingService.Data.State = state;

        await OAuthStateStoreService.SaveAsync<SmartAppSettingModel>(state, SmartAppSettingService.Data, TimeSpan.FromMinutes(10));

        Console.WriteLine($"Generated state: {SmartAppSettingService.Data.State}");
       
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
        return launchUrl;
    }
}
