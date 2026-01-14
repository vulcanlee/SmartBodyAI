using Microsoft.Extensions.Options;
using SmartBodyAI.Models;

namespace SmartBodyAI.Servicers;

public class SettingService
{
    private readonly SettingModel settingModel;

    public SettingService(IOptions<SettingModel> options)
    {
        settingModel = options.Value;
    }

    public SettingModel GetValue()
    {
        return settingModel;
    }
}
