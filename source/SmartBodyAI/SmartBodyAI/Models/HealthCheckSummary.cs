using System.Text.Json;

namespace SmartBodyAI.Models;

public class HealthCheckSummary
{
    public DateTimeOffset CheckedAt { get; set; }
    public int TotalScore { get; set; }
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
    public string? QueryIss { get; set; }
    public string? QueryLaunch { get; set; }
    public bool? QueryDebug { get; set; }
    public string EffectiveFhirServerUrl { get; set; } = string.Empty;
    public string? EffectiveLaunch { get; set; }
    public bool IssOverridesRuntimeFhirServerUrl { get; set; }
    public string? ResolvedAuthorizeUrl { get; set; }
    public string? ResolvedTokenUrl { get; set; }
    public string? FhirMetadataSource { get; set; }
    public SettingModel BoundSetting { get; set; } = new();
    public SmartAppSettingModel RuntimeSetting { get; set; } = new();
    public List<HealthIndicatorResult> Indicators { get; set; } = [];
    public List<ConfigurationFieldDiagnostic> ConfigurationFields { get; set; } = [];
    public List<string> Highlights { get; set; } = [];

    public string BoundSettingJson => JsonSerializer.Serialize(BoundSetting, JsonOptions);
    public string RuntimeSettingJson => JsonSerializer.Serialize(RuntimeSetting, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
