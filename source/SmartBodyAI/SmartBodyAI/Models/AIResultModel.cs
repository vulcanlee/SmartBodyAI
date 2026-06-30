using System.Runtime.Intrinsics.X86;

namespace SmartBodyAI.Models;

public class AIResultModel
{
    public string 骨骼肌面積SMA { get; set; }
    public string 骨骼肌指標SMI { get; set; }
    public string 骨骼肌密度SMD { get; set; }
    public string 骨骼肌綜合指標SMG { get; set; }
    public string 肌間肌肉脂肪組織IMAT { get; set; }
    public string 低密度肌肉區域LAMA { get; set; }
    public string 正常密度肌肉區域NAMA { get; set; }
    public string 肌肉脂肪變性Myosteatosis { get; set; }
}
