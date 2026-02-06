using AIAgent.Models;
using AIAgent.Services;
using CTMS.DataModel.Dtos;
using CTMS.DataModel.Models.AIAgent;
using CTMS.DataModel.Models.ClinicalInformation;
using CTMS.Share.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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

        public DicomPackController(
            IOptions<Agentsetting> agentsettingOptions,
            AgentService agentService)
        {
            this.agentsetting = agentsettingOptions.Value;
            this.agentService = agentService;
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

        public async Task PushToAICheck(string extractPath)
        {
            string result = "";

            string filenameData = "PatientDat.json";
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
