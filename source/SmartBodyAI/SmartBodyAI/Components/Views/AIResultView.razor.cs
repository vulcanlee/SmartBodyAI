using SmartBodyAI.Models;

namespace SmartBodyAI.Components.Views;

public partial class AIResultView
{
    string image1 = "";
    string imageVersion1 = DateTime.Now.Ticks.ToString();
    string image2 = "";
    string imageVersion2 = DateTime.Now.Ticks.ToString();
    AIResultModel aiResultModel = new AIResultModel();

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if(firstRender)
        {
            aiResultModel = new()
            {
                 骨骼肌面積SMA = "126.28",
                 骨骼肌指標SMI = "49.95",
                 骨骼肌密度SMD = "33.73",
                 肌間肌肉脂肪組織IMAT = "13.51",
                 低密度肌肉區域LAMA = "45.48",
                 正常密度肌肉區域NAMA = "80.80",
                 肌肉脂肪變性Myosteatosis = "59.00"
            };
            image1 = "/DicomImages/sample1.png";
            imageVersion1 = DateTime.Now.Ticks.ToString();
            image2 = "/DicomImages/sample2.png";
            imageVersion2 = DateTime.Now.Ticks.ToString();
            StateHasChanged();
        }
    }
}
