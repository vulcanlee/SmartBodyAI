using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace SmartAgentApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DicomPackController : ControllerBase
    {
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
            var baseTempPath = Path.Combine(Path.GetTempPath(), "SmartAgentApi", "DicomPacks");
            Directory.CreateDirectory(baseTempPath);

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
    }
}
