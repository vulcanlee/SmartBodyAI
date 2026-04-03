namespace SmartBodyAI.Models;

public class SmartTokenValidationResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public string FhirUser { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && !string.IsNullOrWhiteSpace(PatientId)
        && !string.IsNullOrWhiteSpace(IdToken)
        && !string.IsNullOrWhiteSpace(FhirUser)
        && Errors.Count == 0;
}
