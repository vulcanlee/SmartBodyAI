using Microsoft.AspNetCore.Components;

namespace SmartBodyAI.Components.Pages;

public partial class PatientInformationPage
{
    [SupplyParameterFromQuery(Name = "code")]
    public string? Code { get; set; }
    [SupplyParameterFromQuery(Name = "state")]
    public string? State { get; set; }
    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }
    [SupplyParameterFromQuery(Name = "error_description")]
    public string? ErrorDescription { get; set; }
}
