using SmartBodyAI.Models;

namespace SmartBodyAI.Servicers;

public class SmartAppSettingService
{
    private readonly SettingService settingService;
    public SmartAppSettingModel Data = new SmartAppSettingModel();

    public SmartAppSettingService(SettingService settingService)
    {
        this.settingService = settingService;

        var data = settingService.GetValue();
        Data.FhirServerUrl = data.FhirServerUrl;
        Data.RedirectUrl = data.RedirectUrl;
        Data.ClientId = data.ClientId;
        Data.ClientSecret = data.ClientSecret;
        Data.ProcessDelayTimeInMilliSeconds = data.ProcessDelayTimeInMilliSeconds;
        Data.AuthorizationScope = data.AuthorizationScope;
        Data.InferenceHostApi = data.InferenceHostApi;
        Data.AIResultPath = data.AIResultPath;
        Data.IsDebug = data.IsDebug;
    }

    public void UpdateSetting(SmartAppSettingModel model)
    {
        Data.AuthorizationScope = model.AuthorizationScope;
        Data.ProcessDelayTimeInMilliSeconds = model.ProcessDelayTimeInMilliSeconds;
        Data.FhirServerUrl = model.FhirServerUrl;
        Data.ClientId = model.ClientId;
        Data.ClientSecret = model.ClientSecret;
        Data.RedirectUrl = model.RedirectUrl;
        Data.AuthCode = model.AuthCode;
        Data.ClientState = model.ClientState;
        Data.TokenUrl = model.TokenUrl;
        Data.AuthorizeUrl = model.AuthorizeUrl;
        Data.Iss = model.Iss;
        Data.Launch = model.Launch;
        Data.State = model.State;
        Data.IsDebug = model.IsDebug;
    }
}
