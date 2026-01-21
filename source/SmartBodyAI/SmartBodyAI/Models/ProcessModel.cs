using System.Runtime.Intrinsics.X86;

namespace SmartBodyAI.Models;

public class ProcessModel
{
    public List<string> ActiveClass { get; set; }= new List<string>();

    public string 確認基本資料 = "";
    public string 上傳DICOM = "";
    public string 進行AI推論 = "";
    public string 查看AI分析 = "";

    public ProcessModel()
    {
        Reset();
        Build();
    }

    public void Reset()
    {
        ActiveClass.Clear();
        ActiveClass.Add("");
        ActiveClass.Add("");
        ActiveClass.Add("");
        ActiveClass.Add("");
    }

    public void Build()
    {
        確認基本資料 = ActiveClass[0];
        上傳DICOM = ActiveClass[1];
        進行AI推論 = ActiveClass[2];
        查看AI分析 = ActiveClass[3];
    }
}
