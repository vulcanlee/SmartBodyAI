using System.Runtime.Intrinsics.X86;

namespace SmartBodyAI.Models;

public class BodyAIResult
{
    public string SMA骨骼肌面積 { get; set; } = "";
    public string SMI骨骼肌指標 { get; set; } = ""; // "低於正常值";
    public string SMD骨骼肌密度 { get; set; } = ""; // "低於正常值";
    public string SMG骨骼肌綜合指標 { get; set; } = ""; // "低於正常值";
    public string IMAT肌間肌肉脂肪組織 { get; set; } = ""; // "高於正常值";
    public string LAMA低密度肌肉區域 { get; set; } = ""; // "正常";
    public string NAMA正常密度肌肉區域 { get; set; } = ""; // "正常";
    public string Myosteatosis肌肉脂肪變性 { get; set; } = ""; // "正常";
    public string CTImage1 { get; set; }
    public string CTImage2 { get; set; }
    /// <summary>
    /// AI分析後的影像檔案名稱
    /// </summary>
    public string Image { get; set; }
    public string ImageExtension { get; set; }
    public string ImageDicom { get { return $"{Image}.dicm"; } }
    public string ImagePng { get; set; }
}
