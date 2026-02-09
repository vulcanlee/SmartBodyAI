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
        private readonly AgentService agentService;
        private readonly AIIntegrateService aiIntegrateService;

        public DicomPackController(
            IOptions<Agentsetting> agentsettingOptions,
            AgentService agentService, AIIntegrateService aIIntegrateService)
        {
            this.agentsetting = agentsettingOptions.Value;
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
                return BadRequest("未收到檔案或檔案內容為空。");
            }

            // 確認副檔名為 .zip
            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("只接受 .zip 檔案。");
            }

            // 準備暫存目錄與解壓縮目錄
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

            // 將上傳的 zip 存到暫存路徑
            await using (var fs = System.IO.File.Create(uploadZipPath))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                // 解壓縮到 extractPath
                ZipFile.ExtractToDirectory(uploadZipPath, extractPath);

                // TODO: 在這裡處理解壓縮後的 DICOM 檔案 (例如：呼叫 service、寫入 DB、push 到 AI 等)
                // 例如：_pushToAiService.ProcessDicomPack(extractPath);

                await PushToAICheck(extractPath);

                return Ok(new
                {
                    Message = "上傳並解壓縮成功。",
                    WorkId = workId,
                    ExtractPath = extractPath
                });
            }
            catch (InvalidDataException)
            {
                return BadRequest("檔案不是有效的 ZIP 格式。");
            }
            catch (Exception ex)
            {
                // 實務上建議記錄 log
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
        }


        [HttpGet]
        [Route("Download/{checkKey}")]
        public async Task<IActionResult> Download(string checkKey)
        {
            string zipDirectoryPath = agentsetting.DicomFolderPath.Replace("Dicom", "Temp");
            string queueFolderPath = agentsetting.QueueFolderPath;
            string completeQueueName = agentsetting.CompleteQueueName;
            string completeQueuePath = Path.Combine(queueFolderPath, completeQueueName);
            string downloadPath = Path.Combine(completeQueuePath, checkKey);

            if (Directory.Exists(zipDirectoryPath) == false)
            {
                Directory.CreateDirectory(zipDirectoryPath);
            }

            // 將 downloadPath 下所有檔案與目錄，都壓縮成為 zip 檔案
            if (Directory.Exists(downloadPath) == false)
            {
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。"
                });
            }

            #region 產生分析結果
            BodyAIResult bodyAIResult = new BodyAIResult();
            string bodyAIResultPath = Path.Combine(downloadPath, "BodyAIResult.json");
            string patientDataPath = Path.Combine(downloadPath, "PatientData.json");
            string content = await System.IO.File.ReadAllTextAsync(patientDataPath);
            PatientAIInfo patientAIInfo = JsonConvert.DeserializeObject<PatientAIInfo>(content);

            InputCsvModel inputCsvModel = await aiIntegrateService.GetInputCsv(checkKey, completeQueuePath);
            string imageRootPath = completeQueuePath;
            var keyName = checkKey;
            // http://localhost:5272/UploadFiles/202509111436154559/Phase1Result/202509111436154559.png
            var imageFilename = $"{keyName}/Phase1Result/{keyName}.png";
            bodyAIResult.ImagePng = imageFilename;
            bodyAIResult.SMD骨骼肌密度 = inputCsvModel.Total_SMD.ToFloat().ToString("F2");
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
            await System.IO.File.WriteAllTextAsync(bodyAIResultPath, bodyAIResultJson);
            #endregion

            string zipFilename = Path.Combine(zipDirectoryPath, $"{checkKey}.zip");
            if (System.IO.File.Exists(zipFilename))
            {
                System.IO.File.Delete(zipFilename);
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(downloadPath, zipFilename);

            // 讀取壓縮檔並回傳
            var fileBytes = await System.IO.File.ReadAllBytesAsync(zipFilename);
            var downloadFileName = $"{checkKey}.zip";
            const string contentType = "application/zip";

            return File(fileBytes, contentType, downloadFileName);
        }

        [HttpGet]
        [Route("CheckResult/{checkKey}")]
        public async Task<IActionResult> CheckResult(string checkKey)
        {
            string queueFolderPath = agentsetting.QueueFolderPath;
            string completeQueueName = agentsetting.CompleteQueueName;
            string completeQueuePath = Path.Combine(queueFolderPath, completeQueueName);
            string checkPath = Path.Combine(completeQueuePath, checkKey);

            if (Directory.Exists(checkPath) == false)
            {
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。"
                });
            }

            var allDirectories = Directory.GetDirectories(checkPath);
            if (allDirectories == null)
            {
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
                return Ok(new
                {
                    Status = true,
                    Message = "AI 推論已完成，結果可供下載。",
                });
            }
            else
            {
                return NotFound(new
                {
                    Status = false,
                    Message = "尚未完成 AI 推論，請稍後再試。",
                });
            }
        }

        public async Task PushToAICheck(string extractPath)
        {
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
