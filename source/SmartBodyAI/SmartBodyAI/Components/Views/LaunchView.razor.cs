using AntDesign;
using Microsoft.AspNetCore.Components;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;
using SmartBodyAI.Services;
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

    [Parameter]
    public string? LaunchCode { get; set; }

    [Inject]
    public INotificationService Notice { get; init; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; init; } = default!;

    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; init; } = default!;

    [Inject]
    public ILogger<LaunchView> logger { get; set; } = default!;

    [Inject]
    public ISmartDiscoveryService SmartDiscoveryService { get; init; } = default!;

    [Inject]
    public ISmartAuthorizationService SmartAuthorizationService { get; init; } = default!;

    public string ProcessingMessage { get; set; } = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            logger.LogInformation("LaunchView 請求 URI：{RequestUri}", NavigationManager.Uri);
            logger.LogInformation("LaunchView 請求參數：Iss={Iss}，Debug={Debug}", Iss, IsDebug);

            SmartAppSettingService.Data.IsDebug = IsDebug;
            if (!string.IsNullOrWhiteSpace(Iss))
            {
                SmartAppSettingService.Data.FhirServerUrl = Iss;
            }

            KeepLaunchIss();
            SmartAppSettingService.UpdateSetting(SmartAppSettingService.Data);
            logger.LogInformation("Launch 設定快照：{Settings}", JsonSerializer.Serialize(SmartAppSettingService.Data));

            _ = Notice.Open(new NotificationConfig
            {
                Message = "SMART 啟動",
                Key = Guid.NewGuid().ToString(),
                Description = "正在準備 SMART on FHIR 獨立授權流程。",
                NotificationType = NotificationType.Info,
                Duration = 5.0,
            });

            if (IsDebug)
            {
                await UpdateMessage("正在解析 SMART 中繼資料。");
            }

            var discovery = await SmartDiscoveryService.DiscoverAsync(SmartAppSettingService.Data.FhirServerUrl);
            if (!discovery.IsValid)
            {
                await UpdateMessage(
                    "SMART 中繼資料錯誤",
                    string.Join(" | ", discovery.ValidationErrors.DefaultIfEmpty("SMART 中繼資料探索失敗。")),
                    NotificationType.Error,
                    4.0);
                return;
            }

            SmartAppSettingService.Data.AuthorizeUrl = discovery.AuthorizeUrl;
            SmartAppSettingService.Data.TokenUrl = discovery.TokenUrl;

            if (IsDebug)
            {
                await UpdateMessage("正在建立 SMART 授權要求。");
            }

            var authorizationRequest = await SmartAuthorizationService.CreateAuthorizationRequestAsync(
                SmartAppSettingService.Data.AuthorizeUrl,
                new SmartAuthorizationRequestContext
                {
                    FhirServerUrl = SmartAppSettingService.Data.FhirServerUrl,
                    AuthorizeUrl = SmartAppSettingService.Data.AuthorizeUrl,
                    TokenUrl = SmartAppSettingService.Data.TokenUrl,
                    ClientId = SmartAppSettingService.Data.ClientId,
                    ClientSecret = SmartAppSettingService.Data.ClientSecret,
                    RedirectUrl = SmartAppSettingService.Data.RedirectUrl,
                    Scope = SmartAppSettingService.Data.AuthorizationScope
                });

            SmartAppSettingService.Data.State = authorizationRequest.State;
            SmartAppSettingService.Data.CodeVerifier = authorizationRequest.CodeVerifier;
            SmartAppSettingService.Data.CodeChallengeMethod = SmartAppSettingModel.DefaultCodeChallengeMethod;
            SmartAppSettingService.Data.AuthorizationError = string.Empty;
            SmartAppSettingService.Data.AuthorizationErrorDescription = string.Empty;

            if (IsDebug)
            {
                await UpdateMessage(
                    "SMART 授權網址",
                    authorizationRequest.AuthorizationUrl,
                    NotificationType.Warning,
                    3.0);
            }

            logger.LogInformation("正在重新導向至授權伺服器：{AuthorizationUrl}", authorizationRequest.AuthorizationUrl);
            await Task.Delay(1000);
            NavigationManager.NavigateTo(authorizationRequest.AuthorizationUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMART 啟動準備失敗。");
            await UpdateMessage("SMART 啟動錯誤", ex.Message, NotificationType.Error, 3.0);
        }
    }

    private async Task UpdateMessage(string description)
    {
        await UpdateMessage("SMART OAuth2 流程", description, NotificationType.Info, 1);
    }

    private async Task UpdateMessage(string message, string description, NotificationType notificationType, double? duration)
    {
        if (notificationType == NotificationType.Error)
        {
            logger.LogError("{Message} - {Description}", message, description);
        }
        else if (notificationType == NotificationType.Warning)
        {
            logger.LogWarning("{Message} - {Description}", message, description);
        }
        else
        {
            logger.LogInformation("{Message} - {Description}", message, description);
        }

        await Notice.Open(new NotificationConfig
        {
            Message = message,
            Key = Guid.NewGuid().ToString(),
            Description = description,
            NotificationType = notificationType,
            Duration = duration,
        });
    }

    public void KeepLaunchIss()
    {
        if (string.IsNullOrEmpty(Iss) || string.IsNullOrEmpty(LaunchCode))
        {
            SmartAppSettingService.Data.Iss = string.Empty;
            SmartAppSettingService.Data.Launch = string.Empty;
            return;
        }

        SmartAppSettingService.Data.Iss = Iss;
        SmartAppSettingService.Data.Launch = LaunchCode;
        SmartAppSettingService.Data.FhirServerUrl = Iss;
    }
}
