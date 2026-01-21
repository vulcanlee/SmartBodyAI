using System.Runtime.Intrinsics.X86;

namespace SmartBodyAI.Models;

//public const string 病歷號輸入 = "影像前處理";
//public const string 上傳影像 = "上傳影像";
//public const string AI分析 = "AI分析";
//public const string 生成報告 = "生成報告";
public enum ProcessStepEnum
{
    確認基本資料 = 0,
    上傳DICOM = 1,
    進行AI推論 = 2,
    查看AI分析 = 3,
    查看結果 = 4,
}
