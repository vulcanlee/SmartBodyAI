using Microsoft.AspNetCore.Components;

namespace SmartBodyAI.Components.Pages
{
    public partial class Home
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; }
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
