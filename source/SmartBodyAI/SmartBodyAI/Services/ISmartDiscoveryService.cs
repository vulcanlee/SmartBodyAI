using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public interface ISmartDiscoveryService
{
    Task<SmartDiscoveryResult> DiscoverAsync(
        string fhirServerUrl,
        IEnumerable<string>? capabilitiesFromMetadata = null,
        CancellationToken cancellationToken = default);
}
