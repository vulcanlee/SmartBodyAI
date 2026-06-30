using AntDesign;
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
    [Inject]
    public ILogger<AIResultView> logger { get; set; }
    [Inject]
    public INotificationService Notice { get; init; }

    bool hasRandomCode = false;
    string image1 = "";
    string imageVersion1 = DateTime.Now.Ticks.ToString();
    string image2 = "";
    string imageVersion2 = DateTime.Now.Ticks.ToString();
    AIResultModel aiResultModel = new AIResultModel();

    override protected void OnInitialized()
    {
        if(string.IsNullOrEmpty(RandomCode))
        {
            logger.LogWarning("RandomCode 必須要提供，不能是空白或者空值.");
            hasRandomCode = false;
        }
        else
        {
            hasRandomCode = true;
        }
    }

    protected override async System.Threading.Tasks.Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if(hasRandomCode == false)
            {
                return;
            }

            string AIResultPath = MagicObjectHelper.UploadDicomTempPath;
            string AIResultZipFile = Path.Combine(AIResultPath, $"{RandomCode}_result.zip");
            string AIResultExtractPath = Path.Combine(AIResultPath, $"{RandomCode}_result");
            if (Directory.Exists(AIResultExtractPath) == false)
            {
                Directory.CreateDirectory(AIResultExtractPath);
            }
            string BodyAIResultJsonPath = Path.Combine(AIResultExtractPath, "BodyAIResult.json");

            if(File.Exists(BodyAIResultJsonPath) == false)
            {
                string message = $"AI 結果的 BodyAIResult.json 檔案不存在，請確認 AI 是否正確執行，或者檔案是否正確上傳。";
                logger.LogWarning(message);

                await Notice.Open(new NotificationConfig()
                {
                    Message = "存取 FHIR 資源",
                    Key = Guid.NewGuid().ToString(),
                    Description = $"{message}",
                    NotificationType = NotificationType.Error,
                    Duration = 5
                });

                StateHasChanged();
                return;
            }

            logger.LogInformation($"開始讀取 AI 結果的 BodyAIResult.json 檔案，路徑: {BodyAIResultJsonPath}");
            string content = System.IO.File.ReadAllText(BodyAIResultJsonPath);
            BodyAIResult bodyAIResult = JsonConvert.DeserializeObject<BodyAIResult>(content);

            string sourceAIResultImagePath = Path.Combine(AIResultExtractPath, "Phase2Result", $"{RandomCode}_muscle5.png");
            string sourceAIResultImageFilename = $"{RandomCode}_muscle5.png";
            string targetImagePath = Path.Combine(MagicObjectHelper.DicomImagePath, sourceAIResultImageFilename);

            logger.LogInformation($"開始複製 AI 結果的影像檔案，來源路徑: {sourceAIResultImagePath}，目標路徑: {targetImagePath}");
            File.Copy(sourceAIResultImagePath, targetImagePath, true);

            aiResultModel = new()
            {
                骨骼肌面積SMA = bodyAIResult.SMA骨骼肌面積,
                骨骼肌指標SMI = bodyAIResult.SMI骨骼肌指標,
                骨骼肌密度SMD = bodyAIResult.SMD骨骼肌密度,
                骨骼肌綜合指標SMG = bodyAIResult.SMG骨骼肌綜合指標,
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
