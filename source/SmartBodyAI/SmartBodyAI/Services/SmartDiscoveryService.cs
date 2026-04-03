using System.Text.Json;
using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public class SmartDiscoveryService : ISmartDiscoveryService
{
    private const string OAuthUrisExtensionUrl = "http://fhir-registry.smarthealthit.org/StructureDefinition/oauth-uris";
    private const string CapabilitiesExtensionUrl = "http://fhir-registry.smarthealthit.org/StructureDefinition/capabilities";
    private static readonly string[] RequiredStandaloneCapabilities =
    [
        "launch-standalone",
        "context-standalone-patient",
        "permission-patient",
        "sso-openid-connect"
    ];

    private readonly Func<HttpClient> clientFactory;

    public SmartDiscoveryService(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory.CreateClient)
    {
    }

    public SmartDiscoveryService(Func<HttpClient> clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    public async Task<SmartDiscoveryResult> DiscoverAsync(
        string fhirServerUrl,
        IEnumerable<string>? capabilitiesFromMetadata = null,
        CancellationToken cancellationToken = default)
    {
        SmartDiscoveryResult result = new();

        if (string.IsNullOrWhiteSpace(fhirServerUrl))
        {
            result.ValidationErrors.Add("FHIR server URL is required.");
            return result;
        }

        var baseUrl = fhirServerUrl.TrimEnd('/');
        using var client = clientFactory();

        if (await TryReadWellKnownAsync(client, $"{baseUrl}/.well-known/smart-configuration", result, cancellationToken))
        {
            ValidateCapabilities(result);
            return result;
        }

        await TryReadMetadataAsync(client, $"{baseUrl}/metadata", result, capabilitiesFromMetadata, cancellationToken);
        ValidateCapabilities(result);
        return result;
    }

    private static async Task<bool> TryReadWellKnownAsync(
        HttpClient client,
        string url,
        SmartDiscoveryResult result,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        result.MetadataSource = ".well-known/smart-configuration";
        result.AuthorizeUrl = document.RootElement.TryGetProperty("authorization_endpoint", out var auth)
            ? auth.GetString() ?? string.Empty
            : string.Empty;
        result.TokenUrl = document.RootElement.TryGetProperty("token_endpoint", out var token)
            ? token.GetString() ?? string.Empty
            : string.Empty;

        if (document.RootElement.TryGetProperty("capabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Array)
        {
            foreach (var capability in capabilities.EnumerateArray())
            {
                var value = capability.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Capabilities.Add(value);
                }
            }
        }

        return true;
    }

    private static async Task TryReadMetadataAsync(
        HttpClient client,
        string url,
        SmartDiscoveryResult result,
        IEnumerable<string>? capabilitiesFromMetadata,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            result.ValidationErrors.Add($"Failed to retrieve SMART metadata from {url}. HTTP {(int)response.StatusCode}.");
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        result.MetadataSource = "/metadata";

        foreach (var rest in EnumerateArray(document.RootElement, "rest"))
        {
            if (!rest.TryGetProperty("security", out var security))
            {
                continue;
            }

            foreach (var extension in EnumerateArray(security, "extension"))
            {
                var urlValue = extension.TryGetProperty("url", out var extensionUrl)
                    ? extensionUrl.GetString()
                    : null;

                if (string.Equals(urlValue, OAuthUrisExtensionUrl, StringComparison.Ordinal))
                {
                    foreach (var oauthExtension in EnumerateArray(extension, "extension"))
                    {
                        var key = oauthExtension.TryGetProperty("url", out var nestedUrl)
                            ? nestedUrl.GetString()
                            : null;
                        var value = oauthExtension.TryGetProperty("valueUri", out var valueUri)
                            ? valueUri.GetString()
                            : null;

                        if (string.Equals(key, "authorize", StringComparison.Ordinal))
                        {
                            result.AuthorizeUrl = value ?? string.Empty;
                        }
                        else if (string.Equals(key, "token", StringComparison.Ordinal))
                        {
                            result.TokenUrl = value ?? string.Empty;
                        }
                    }
                }

                if (string.Equals(urlValue, CapabilitiesExtensionUrl, StringComparison.Ordinal))
                {
                    foreach (var capabilityExtension in EnumerateArray(extension, "extension"))
                    {
                        var capability = capabilityExtension.TryGetProperty("valueCode", out var valueCode)
                            ? valueCode.GetString()
                            : null;

                        if (!string.IsNullOrWhiteSpace(capability))
                        {
                            result.Capabilities.Add(capability);
                        }
                    }
                }
            }
        }

        if (result.Capabilities.Count == 0 && capabilitiesFromMetadata is not null)
        {
            result.Capabilities.AddRange(capabilitiesFromMetadata.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement parent, string propertyName)
    {
        if (parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static void ValidateCapabilities(SmartDiscoveryResult result)
    {
        if (string.IsNullOrWhiteSpace(result.AuthorizeUrl))
        {
            result.ValidationErrors.Add("SMART discovery did not provide authorization_endpoint.");
        }

        if (string.IsNullOrWhiteSpace(result.TokenUrl))
        {
            result.ValidationErrors.Add("SMART discovery did not provide token_endpoint.");
        }

        foreach (var requiredCapability in RequiredStandaloneCapabilities)
        {
            if (!result.Capabilities.Contains(requiredCapability, StringComparer.Ordinal))
            {
                result.ValidationErrors.Add($"SMART capability '{requiredCapability}' is required for standalone OIDC launch.");
            }
        }
    }
}
