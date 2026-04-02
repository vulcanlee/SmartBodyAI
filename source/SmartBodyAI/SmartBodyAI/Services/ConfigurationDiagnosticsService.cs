using Microsoft.Extensions.Configuration;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public class ConfigurationDiagnosticsService
{
    private readonly IConfiguration configuration;
    private readonly IConfigurationRoot configurationRoot;
    private readonly IConfiguration appSettingsConfiguration;

    public ConfigurationDiagnosticsService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        this.configuration = configuration;
        configurationRoot = (IConfigurationRoot)configuration;
        appSettingsConfiguration = new ConfigurationBuilder()
            .SetBasePath(environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }

    public List<ConfigurationFieldDiagnostic> BuildDiagnostics(
        SettingModel boundSetting,
        SmartAppSettingModel runtimeSetting,
        string? queryIss,
        string? queryLaunch,
        bool? queryDebug)
    {
        return CreateDefinitions()
            .Select(definition => CreateDiagnostic(definition, boundSetting, runtimeSetting, queryIss, queryLaunch, queryDebug))
            .ToList();
    }

    private ConfigurationFieldDiagnostic CreateDiagnostic(
        ConfigurationFieldDefinition definition,
        SettingModel boundSetting,
        SmartAppSettingModel runtimeSetting,
        string? queryIss,
        string? queryLaunch,
        bool? queryDebug)
    {
        string key = $"{MagicObjectHelper.SmartAppSettingKey}:{definition.PropertyName}";
        string? finalValue = configuration[key];
        string? appSettingsValue = appSettingsConfiguration[key];
        string? runtimeValue = definition.RuntimeAccessor(runtimeSetting);
        string? queryValue = definition.QueryAccessor(queryIss, queryLaunch, queryDebug);
        string? providerName = GetWinningProviderName(key, out bool fromEnvironmentVariable);
        bool overriddenByQuery = !string.IsNullOrWhiteSpace(queryValue)
            && !string.Equals(queryValue, runtimeValue, StringComparison.Ordinal);

        return new ConfigurationFieldDiagnostic
        {
            Key = key,
            Label = definition.Label,
            Exists = !string.IsNullOrWhiteSpace(finalValue),
            FinalValue = finalValue,
            AppSettingsValue = appSettingsValue,
            RuntimeValue = runtimeValue,
            QueryValue = queryValue,
            SourceProvider = providerName,
            FromEnvironmentVariable = fromEnvironmentVariable,
            DiffersFromAppSettings = !string.Equals(finalValue, appSettingsValue, StringComparison.Ordinal),
            MatchesRuntimeValue = string.Equals(finalValue, runtimeValue, StringComparison.Ordinal),
            OverriddenByQuery = overriddenByQuery,
            Notes = BuildNotes(finalValue, appSettingsValue, runtimeValue, queryValue, providerName, fromEnvironmentVariable, overriddenByQuery)
        };
    }

    private string BuildNotes(
        string? finalValue,
        string? appSettingsValue,
        string? runtimeValue,
        string? queryValue,
        string? providerName,
        bool fromEnvironmentVariable,
        bool overriddenByQuery)
    {
        List<string> notes = [];

        if (string.IsNullOrWhiteSpace(finalValue))
        {
            notes.Add("Final configuration value is empty.");
        }

        if (fromEnvironmentVariable)
        {
            notes.Add("Final value is coming from environment variables.");
        }
        else if (!string.IsNullOrWhiteSpace(providerName))
        {
            notes.Add($"Winning provider: {providerName}.");
        }

        if (!string.Equals(finalValue, appSettingsValue, StringComparison.Ordinal))
        {
            notes.Add("Final value is different from appsettings.json.");
        }

        if (!string.Equals(finalValue, runtimeValue, StringComparison.Ordinal))
        {
            notes.Add("Runtime SMART setting differs from the bound configuration.");
        }

        if (overriddenByQuery)
        {
            notes.Add("Current request query would override the runtime value used by the SMART flow.");
        }
        else if (!string.IsNullOrWhiteSpace(queryValue))
        {
            notes.Add("Current request query matches the runtime SMART value.");
        }

        return string.Join(" ", notes);
    }

    private string? GetWinningProviderName(string key, out bool fromEnvironmentVariable)
    {
        fromEnvironmentVariable = false;

        foreach (IConfigurationProvider provider in configurationRoot.Providers.Reverse())
        {
            if (!provider.TryGet(key, out string? _))
            {
                continue;
            }

            string providerName = provider.ToString() ?? provider.GetType().Name;
            fromEnvironmentVariable = provider.GetType().FullName?.Contains("EnvironmentVariables", StringComparison.OrdinalIgnoreCase) == true
                || providerName.Contains("EnvironmentVariables", StringComparison.OrdinalIgnoreCase);

            return providerName;
        }

        return null;
    }

    private static List<ConfigurationFieldDefinition> CreateDefinitions()
    {
        return
        [
            new("FhirServerUrl", "FHIR Server URL", setting => setting.FhirServerUrl, (iss, _, _) => iss),
            new("ClientId", "Client ID", setting => setting.ClientId, (_, _, _) => null),
            new("ClientSecret", "Client Secret", setting => setting.ClientSecret, (_, _, _) => null),
            new("RedirectUrl", "Redirect URL", setting => setting.RedirectUrl, (_, _, _) => null),
            new("AuthorizationScope", "Authorization Scope", setting => setting.AuthorizationScope, (_, _, _) => null),
            new("InferenceHostApi", "Inference Host API", setting => setting.InferenceHostApi, (_, _, _) => null),
            new("UploadDicomPath", "Upload DICOM Path", _ => null, (_, _, _) => null),
            new("UploadDicomTempPath", "Upload DICOM Temp Path", _ => null, (_, _, _) => null),
            new("DicomImagePath", "DICOM Image Path", _ => null, (_, _, _) => null),
            new("AIResultPath", "AI Result Path", setting => setting.AIResultPath, (_, _, _) => null),
            new("IsDebug", "Debug Mode", setting => setting.IsDebug.ToString(), (_, _, debug) => debug?.ToString())
        ];
    }

    private sealed record ConfigurationFieldDefinition(
        string PropertyName,
        string Label,
        Func<SmartAppSettingModel, string?> RuntimeAccessor,
        Func<string?, string?, bool?, string?> QueryAccessor);
}
