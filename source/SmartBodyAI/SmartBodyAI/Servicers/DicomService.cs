using FellowOakDicom;
using FellowOakDicom.Imaging;
using SixLabors.ImageSharp.Formats.Png;

namespace SmartBodyAI.Servicers;

public class DicomService
{
    private readonly Microsoft.Extensions.Logging.ILogger<DicomService> logger;

    public DicomService(Microsoft.Extensions.Logging.ILogger<DicomService> logger)
    {
        this.logger = logger;

        InitializeDicom();
    }

    private void InitializeDicom()
    {
        try
        {
            // 在應用程式啟動時初始化
            //new DicomSetupBuilder()
            //  .RegisterServices(s => s.AddFellowOakDicom()
            //  .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>())
            //  .SkipValidation()
            //  .Build();

            new DicomSetupBuilder()
                .RegisterServices(s =>
                s.AddFellowOakDicom()
                 .AddTranscoderManager<FellowOakDicom.Imaging.NativeCodec.NativeTranscoderManager>()
                 .AddImageManager<ImageSharpImageManager>())
          .SkipValidation()
          .Build();

            //new DicomSetupBuilder()
            //    .RegisterServices(s => s.AddFellowOakDicom()
            //    .AddImageManager<ImageSharpImageManager>())
            //    .SkipValidation()
            //    .Build();

            logger.LogInformation("DICOM 初始化成功");
        }
        catch (Exception ex)
        {
            logger.LogError($"DICOM 初始化失敗: {ex.Message}");
            throw;
        }
    }

    public void ConvertSingleFile(string dicomPath, string pngPath)
    {
        try
        {
            if (File.Exists(pngPath))
            {
                File.Delete(pngPath);
            }
            logger.LogInformation($"開始轉換: {dicomPath}");

            // 開啟 DICOM 檔案
            var dicomFile = DicomFile.Open(dicomPath);
            var dicomImage = new DicomImage(dicomFile.Dataset);

            // 渲染影像
            //var foo = dicomImage.RenderImage().As<Bitmap>();
            var renderedImage = dicomImage.RenderImage();

            // 將 renderedImage 轉換為 ImageSharp Image 並儲存為 PNG
            var sharpImage = renderedImage.AsSharpImage();

            // 儲存為 PNG
            using (var fileStream = new FileStream(pngPath, FileMode.Create))
            {
                sharpImage.Save(fileStream, new PngEncoder());
            }

            logger.LogInformation($"轉換完成: {pngPath}");

            // 顯示影像資訊
            //DisplayImageInfo(dicomFile.Dataset, sharpImage);
        }
        catch (Exception ex)
        {
            logger.LogError($"轉換失敗: {ex.Message}");
            throw;
        }
    }

}
