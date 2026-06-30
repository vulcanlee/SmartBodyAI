using AIAgent.Models;
using AIAgent.Services;
using CTMS.Business.Services.ClinicalInformation;
using CTMS.DataModel.Dtos;
using CTMS.DataModel.Models.AIAgent;
using CTMS.DataModel.Models.ClinicalInformation;
using CTMS.Share.Extensions;
using CTMS.Share.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SmartAgentApi.Models;
using System.IO.Compression;
using System.Text.Json.Serialization;

namespace SmartAgentApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DicomPackController : ControllerBase
    {
        private Agentsetting agentsetting;
        private readonly ILogger<DicomPackController> logger;
        private readonly AgentService agentService;
        private readonly AIIntegrateService aiIntegrateService;
        string logMessage = string.Empty;

        public DicomPackController(ILogger<DicomPackController> logger,
            IOptions<Agentsetting> agentsettingOptions,
            AgentService agentService, AIIntegrateService aIIntegrateService)
        {
            this.agentsetting = agentsettingOptions.Value;
            this.logger = logger;
            this.agentService = agentService;
            this.aiIntegrateService = aIIntegrateService;
        }
        /// <summary>
        /// 上傳一個 ZIP 檔案並解壓縮到伺服端暫存資料夾。
        /// </summary>
        /// <param name="file">用 form-data 上傳的 zip 檔案欄位名稱為 "file"</param>
        [HttpPost]
        [RequestSizeLimit(1_000_000_000)] // 可視需求調整最大上傳大小
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                logMessage = $"未收到檔案或檔案內容為空。";
                logger.LogWarning(logMessage);
                return BadRequest("未收到檔案或檔案內容為空。");
            }

            logger.LogInformation($"收到檔案：{file.FileName}，大小：{file.Length} bytes");

            #region 確認副檔名為 .zip
            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                logMessage = $"只接受 .zip 檔案，收到的檔案副檔名為 {ext}。";
                logger.LogWarning(logMessage);
                return BadRequest("只接受 .zip 檔案。");
            }
            #endregion

            #region 準備暫存目錄與解壓縮目錄
            logMessage = "準備暫存目錄與解壓縮目錄";
            logger.LogWarning(logMessage);
            string UploadDicomTempPath = $"C:\\temp\\SmartBodyAI\\TempUploads";
            if (!Directory.Exists(UploadDicomTempPath))
            {
                Directory.CreateDirectory(UploadDicomTempPath);
            }
            var baseTempPath = Path.Combine(UploadDicomTempPath, "DicomPacks");
            if (!Directory.Exists(baseTempPath))
            {
                Directory.CreateDirectory(baseTempPath);
            }

            // 以時間戳 + Guid 建立獨立工作目錄，避免併發衝突
            var workId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            var uploadZipPath = Path.Combine(baseTempPath, $"{workId}.zip");
            var extractPath = Path.Combine(baseTempPath, workId);

            Directory.CreateDirectory(extractPath);
            #endregion

            #region 將上傳的 zip 存到暫存路徑
            logMessage = $"將上傳的 zip 存到暫存路徑 {uploadZipPath}";
            logger.LogInformation(logMessage);
            await using (var fs = System.IO.File.Create(uploadZipPath))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                logger.LogInformation($"開始解壓縮 {uploadZipPath} 到 {extractPath}");
                ZipFile.ExtractToDirectory(uploadZipPath, extractPath);

                logger.LogInformation($"解壓縮完成，開始處理 DICOM 檔案並推送到 AI。");
                await PushToAICheck(extractPath);

                logMessage = $"上傳並解壓縮成功，工作 ID: {workId}，解壓縮路徑: {extractPath}";
                logger.LogInformation(logMessage);
                return Ok(new
                {
                    Message = "上傳並解壓縮成功。",
                    WorkId = workId,
                    ExtractPath = extractPath
                });
            }
            catch (InvalidDataException)
            {
                logger.LogError($"檔案 {uploadZipPath} 不是有效的 ZIP 格式。");
                return BadRequest("檔案不是有效的 ZIP 格式。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"處理 ZIP 檔案時發生錯誤：{ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Message = "處理 ZIP 檔案時發生錯誤。",
                    Error = ex.Message
                });
            }
            finally
            {
                // 視需求決定是否刪除原始 zip
                // 如果後續不再需要，可在這裡刪除
                try
                {
                    if (System.IO.File.Exists(uploadZipPath))
                    {
                        System.IO.File.Delete(uploadZipPath);
                    }
                }
                catch
                {
                    // 忽略刪除失敗
                }
            }
            #endregion
        }


        [HttpGet]
        [Route("Download/{checkKey}")]
        public async Task<IActionResult> Download(string checkKey)
        {
            logger.LogInformation($"收到下載請求，checkKey: {checkKey}");
            string zipDirectoryPath = agentsetting.DicomFolderPath.Replace("Dicom", "Temp");
            string queueFolderPath = agentsetting.QueueFolderPath;
            string completeQueueName = agentsetting.CompleteQueueName;
            string completeQueuePath = Path.Combine(queueFolderPath, completeQueueName);
            string downloadPath = Path.Combine(completeQueuePath, checkKey);

            if (Directory.Exists(zipDirectoryPath) == false)
            {
                Directory.CreateDirectory(zipDirectoryPath);
            }

            logger.LogInformation($"將 downloadPath 下所有檔案與目錄，都壓縮成為 zip 檔案");
            if (Directory.Exists(downloadPath) == false)
            {
                logger.LogWarning($"下載路徑 {downloadPath} 不存在，可能尚未完成 AI 推論。");
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。"
                });
            }

            #region 產生分析結果
            logger.LogInformation($"開始產生分析結果 JSON 檔案");
            BodyAIResult bodyAIResult = new BodyAIResult();
            string bodyAIResultPath = Path.Combine(downloadPath, "BodyAIResult.json");
            string patientDataPath = Path.Combine(downloadPath, "PatientData.json");
            string content = await System.IO.File.ReadAllTextAsync(patientDataPath);
            PatientAIInfo patientAIInfo = JsonConvert.DeserializeObject<PatientAIInfo>(content);

            logger.LogInformation($"從AI推論的結果 csv 檔案，讀取 Phase3Result > input.csv 內容");
            InputCsvModel inputCsvModel = await aiIntegrateService.GetInputCsv(checkKey, completeQueuePath);
            string imageRootPath = completeQueuePath;
            var keyName = checkKey;
            // http://localhost:5272/UploadFiles/202509111436154559/Phase1Result/202509111436154559.png
            var imageFilename = $"{keyName}/Phase1Result/{keyName}.png";
            bodyAIResult.ImagePng = imageFilename;
            bodyAIResult.SMD骨骼肌密度 = inputCsvModel.Total_SMD.ToFloat().ToString("F2");
            bodyAIResult.SMG骨骼肌綜合指標 = inputCsvModel.Total_SMG.ToFloat().ToString("F2");
            bodyAIResult.IMAT肌間肌肉脂肪組織 = inputCsvModel.Total_ImatA.ToFloat().ToString("F2");
            bodyAIResult.LAMA低密度肌肉區域 = inputCsvModel.Total_LamaA.ToFloat().ToString("F2");
            bodyAIResult.NAMA正常密度肌肉區域 = inputCsvModel.Total_NamaA.ToFloat().ToString("F2");
            // SMA : SMA (Skeletal Muscle Area) TotalLamaA + TotalNamaA 骨骼肌面積
            bodyAIResult.SMA骨骼肌面積 = (inputCsvModel.Total_LamaA.ToFloat() + inputCsvModel.Total_NamaA.ToFloat()).ToString("F2");
            //SMI= LAMA+NAMA/(身高的平方(公尺))
            bodyAIResult.SMI骨骼肌指標 = ((inputCsvModel.Total_LamaA.ToFloat() + inputCsvModel.Total_NamaA.ToFloat())
                / (patientAIInfo.Height.ToFloat() / 100.0 * patientAIInfo.Height.ToFloat() / 100.0)).ToString("F2");

            bodyAIResult.Myosteatosis肌肉脂肪變性 = (inputCsvModel.Total_ImatA.ToFloat() + inputCsvModel.Total_LamaA.ToFloat()).ToString("F2");

            string bodyAIResultJson = JsonConvert.SerializeObject(bodyAIResult, Formatting.Indented);
            logger.LogInformation($"將分析結果寫入 JSON 檔案 {bodyAIResultPath}");
            await System.IO.File.WriteAllTextAsync(bodyAIResultPath, bodyAIResultJson);
            #endregion

            string zipFilename = Path.Combine(zipDirectoryPath, $"{checkKey}.zip");
            if (System.IO.File.Exists(zipFilename))
            {
                System.IO.File.Delete(zipFilename);
            }
            logger.LogInformation($"將 {downloadPath} 下的所有檔案與目錄壓縮成 zip 檔案 {zipFilename}");
            System.IO.Compression.ZipFile.CreateFromDirectory(downloadPath, zipFilename);

            logger.LogInformation($"準備將 zip 檔案 {zipFilename} 送回給前端下載");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipFilename);
            var downloadFileName = $"{checkKey}.zip";
            const string contentType = "application/zip";

            return File(fileBytes, contentType, downloadFileName);
        }

        [HttpGet]
        [Route("CheckResult/{checkKey}")]
        public async Task<IActionResult> CheckResult(string checkKey)
        {
            logger.LogInformation($"收到檢查結果請求，checkKey: {checkKey}");
            string queueFolderPath = agentsetting.QueueFolderPath;
            string completeQueueName = agentsetting.CompleteQueueName;
            string completeQueuePath = Path.Combine(queueFolderPath, completeQueueName);
            string checkPath = Path.Combine(completeQueuePath, checkKey);

            if (Directory.Exists(checkPath) == false)
            {
                logger.LogWarning($"檢查路徑 {checkPath} 不存在，可能尚未完成 AI 推論。");
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。"
                });
            }

            var allDirectories = Directory.GetDirectories(checkPath);
            if (allDirectories == null)
            {
                logger.LogWarning($"檢查路徑 {checkPath} 下沒有任何子目錄，可能尚未完成 AI 推論。");
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。"
                });
            }

            var requiredKeywords = new[] { "Phase1Result", "Phase2Result", "Phase3Result" };

            // 只要所有項目內，有這三個字串內的任一個，就視為 true
            var isAllMatched = allDirectories.All(dir =>
                requiredKeywords.Any(kw => dir.Contains(kw, StringComparison.OrdinalIgnoreCase)));

            if (isAllMatched)
            {
                logger.LogInformation($"檢查路徑 {checkPath} 下的子目錄包含所有必要的階段結果，AI 推論已完成。");
                return Ok(new
                {
                    Status = true,
                    Message = "AI 推論已完成，結果可供下載。",
                });
            }
            else
            {
                logger.LogWarning($"檢查路徑 {checkPath} 下的子目錄不包含所有必要的階段結果，可能尚未完成 AI 推論。");
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。",
                });
            }
        }

        public async Task PushToAICheck(string extractPath)
        {
            logger.LogInformation($"開始將解壓縮後的 DICOM 檔案與患者資料推送到 AI，extractPath: {extractPath}");
            string result = "";

            string filenameData = "PatientData.json";
            string filenameDicom = "L3CT.dicm";
            string pathSourceData = Path.Combine(extractPath, filenameData);
            string pathSourceDicom = Path.Combine(extractPath, filenameDicom);

            string content = await System.IO.File.ReadAllTextAsync(pathSourceData);
            PatientAIInfo patientAIInfo = JsonConvert.DeserializeObject<PatientAIInfo>(content);

            patientAIInfo.癌別 = "EC";
            // 取得 extractPath 最後一個目錄名稱
            patientAIInfo.KeyName = patientAIInfo.Code;

            string sourcePath = pathSourceDicom;
            string targetPath = sourcePath.Replace(filenameDicom, $"{patientAIInfo.KeyName}.dicm");

            logger.LogInformation($"將 DICOM 檔案從 {sourcePath} 複製到 {targetPath}");
            System.IO.File.Copy(sourcePath, targetPath, true);
            patientAIInfo.DicomFilename = targetPath;

            //var currentRootPath = Directory.GetCurrentDirectory();
            //var dicmTempRootPath = Path.Combine(currentRootPath, MagicObjectHelper.UploadTempPath);
            //var sourceDicomFilePath = Path.GetDirectoryName(targetPath);
            //var destinationDicomFileName = Path.Combine(dicmTempRootPath, $"{patientAIInfo.KeyName}.dcm");
            //System.IO.File.Copy(targetPath, destinationDicomFileName, true);

            agentService.CreateInBound(patientAIInfo, agentsetting);
        }
    }
}
