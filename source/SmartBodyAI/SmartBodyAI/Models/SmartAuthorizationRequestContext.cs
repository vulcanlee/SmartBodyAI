namespace SmartBodyAI.Models;

public class SmartAuthorizationRequestContext
{
    public string FhirServerUrl { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
