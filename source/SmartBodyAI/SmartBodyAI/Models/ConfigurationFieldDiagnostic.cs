namespace SmartBodyAI.Models;

public class ConfigurationFieldDiagnostic
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string? FinalValue { get; set; }
    public string? AppSettingsValue { get; set; }
    public string? RuntimeValue { get; set; }
    public string? QueryValue { get; set; }
    public string? SourceProvider { get; set; }
    public bool FromEnvironmentVariable { get; set; }
    public bool DiffersFromAppSettings { get; set; }
    public bool MatchesRuntimeValue { get; set; }
    public bool OverriddenByQuery { get; set; }
    public string Notes { get; set; } = string.Empty;
}
