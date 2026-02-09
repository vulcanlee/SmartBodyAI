using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Newtonsoft.Json;
using SmartBodyAI.Helpers;
using SmartBodyAI.Models;
using SmartBodyAI.Servicers;

namespace SmartBodyAI.Components.Views;

public partial class AIResultView
{
    [Parameter]
    public string RandomCode { get; set; }
    [Inject]
    public SmartAppSettingService SmartAppSettingService { get; set; }
    string image1 = "";
    string imageVersion1 = DateTime.Now.Ticks.ToString();
    string image2 = "";
    string imageVersion2 = DateTime.Now.Ticks.ToString();
    AIResultModel aiResultModel = new AIResultModel();

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            string AIResultPath = SmartAppSettingService.Data.AIResultPath;
            string AIResultZipFile = Path.Combine(AIResultPath, $"{RandomCode}.zip");
            string AIResultExtractPath = Path.Combine(AIResultPath, $"{RandomCode}");
            if (Directory.Exists(AIResultExtractPath) == false)
            {
                Directory.CreateDirectory(AIResultExtractPath);
            }
            string BodyAIResultJsonPath = Path.Combine(AIResultExtractPath, "BodyAIResult.json");
            string content = System.IO.File.ReadAllText(BodyAIResultJsonPath);
            BodyAIResult bodyAIResult = JsonConvert.DeserializeObject<BodyAIResult>(content);

            string sourceAIResultImagePath = Path.Combine(AIResultExtractPath, "Phase2Result", $"{RandomCode}_muscle5.png");
            string sourceAIResultImageFilename = $"{RandomCode}_muscle5.png";
            string targetImagePath = Path.Combine(MagicObjectHelper.DicomImagePath, sourceAIResultImageFilename);
            File.Copy(sourceAIResultImagePath, targetImagePath, true);

            aiResultModel = new()
            {
                骨骼肌面積SMA = bodyAIResult.SMA骨骼肌面積,
                骨骼肌指標SMI = bodyAIResult.SMI骨骼肌指標,
                骨骼肌密度SMD = bodyAIResult.SMD骨骼肌密度,
                肌間肌肉脂肪組織IMAT = bodyAIResult.IMAT肌間肌肉脂肪組織,
                低密度肌肉區域LAMA = bodyAIResult.LAMA低密度肌肉區域,
                正常密度肌肉區域NAMA = bodyAIResult.NAMA正常密度肌肉區域,
                肌肉脂肪變性Myosteatosis = bodyAIResult.Myosteatosis肌肉脂肪變性
            };
            
            image1 = $"/DicomImages/{RandomCode}.png";
            imageVersion1 = DateTime.Now.Ticks.ToString();
            image2 = $"/DicomImages/{RandomCode}_muscle5.png";
            imageVersion2 = DateTime.Now.Ticks.ToString();
            StateHasChanged();
        }
    }
}
