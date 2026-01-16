namespace SmartBodyAI.Models;

public class SettingModel
{
    public string FhirServerUrl { get; set; }
    public string RedirectUrl { get; set; }
    public string ClientId { get; set; }
    public string AuthorizationScope { get; set; }
    public int ProcessDelayTimeInMilliSeconds { get; set; }
    public string UploadDicomPath { get; set; }
    public string UploadDicomTempPath { get; set; }
    public string DicomImagePath { get; set; }
}
