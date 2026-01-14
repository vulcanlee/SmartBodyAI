using Microsoft.AspNetCore.Components;

namespace SmartBodyAI.Components.Pages;

public partial class PatientInformationPage
{
    [SupplyParameterFromQuery(Name = "code")]
    public string? Code { get; set; }
    [SupplyParameterFromQuery(Name = "state")]
    public string? State { get; set; }
}
