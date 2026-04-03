namespace SmartBodyAI.Models;

public class SmartAuthorizationRequest
{
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
}
