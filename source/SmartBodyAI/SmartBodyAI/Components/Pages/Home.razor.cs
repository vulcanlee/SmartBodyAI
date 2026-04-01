using Microsoft.AspNetCore.Components;

namespace SmartBodyAI.Components.Pages
{
    public partial class Home
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; }
        [SupplyParameterFromQuery(Name = "debug")]
        public bool IsDebug { get; set; } = true;
        [SupplyParameterFromQuery(Name = "iss")]
        public string Iss { get; set; } = string.Empty;
        [SupplyParameterFromQuery(Name = "launch")]
        public string Launch { get; set; } = string.Empty;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                //await Task.Delay(3000);
                //NavigationManager.NavigateTo("/launch");
            }
        }
    }
}
