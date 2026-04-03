namespace SmartBodyAI.Models;

public class SmartDiscoveryResult
{
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string MetadataSource { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = [];
    public List<string> ValidationErrors { get; set; } = [];

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AuthorizeUrl)
        && !string.IsNullOrWhiteSpace(TokenUrl)
        && ValidationErrors.Count == 0;
}
