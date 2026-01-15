using Microsoft.AspNetCore.Components;
using SmartBodyAI.Helpers;
using Syncfusion.Blazor.Inputs;

namespace SmartBodyAI.Components.Commons;

public partial class UploadDicomDialog
{
    [Parameter]
    public bool OpenPicker { get; set; } = false;
    [Parameter]
    public string SubjectNo { get; set; }
    [Parameter]
    public EventCallback<string> OnConfirmCallback { get; set; }

    SfUploader upload1;

    string DialogTitle = "上傳 DICOM 檔案";
    public bool ShowMessageBox { get; set; } = false;
    public string MessageBoxBody { get; set; } = "";
    public string MessageBoxTitle { get; set; } = "";

    bool ShowAddPatientDialog = false;

    protected override async Task OnInitializedAsync()
    {
        await GetData();
    }

    async Task GetData()
    {
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
        }
    }

    async Task OnPickerOK()
    {
    }

    async Task OnPickerCancel()
    {
        OpenPicker = false;
        await OnConfirmCallback.InvokeAsync(null);
    }


    public async Task UploaderChange(UploadChangeEventArgs args)
    {
        await Task.Delay(100);
        var sourceDicomFileName = "";
        var sourceDicomPath = "";
        var targetImageFileName = "";
        var targetImagePath = "";
        foreach (var uploadFile in args.Files)
        {
            var file = uploadFile.File;
            try
            {
            }
            catch (Exception ex)
            {
            }
            using (var inputStream = file.OpenReadStream(20 * 1024 * 1024))
            {
                sourceDicomFileName = $"{SubjectNo}.dicm";

                // 將 inputFileName 內容複製到指定 path 目錄下，檔案名稱要相同
                sourceDicomPath = Path.Combine(MagicObjectHelper.UploadDicomPath, sourceDicomFileName);
                using (var fileStream = new FileStream(sourceDicomPath, FileMode.Create, FileAccess.Write))
                {
                    await inputStream.CopyToAsync(fileStream);
                }
            }

            #region 將DICOM 轉成圖片
            string pngFile = $"{SubjectNo}.png";
            string dicomFilePath = sourceDicomPath;
            string pngFilePath = Path.Combine(MagicObjectHelper.DicomImagePath, pngFile);

            // 轉換單個檔案
            DicomService.ConvertSingleFile(dicomFilePath, pngFilePath);
            #endregion

            OpenPicker = false;
            await OnConfirmCallback.InvokeAsync(sourceDicomPath);
            break;
        }
        await Task.Delay(100);
        StateHasChanged();
    }
}
