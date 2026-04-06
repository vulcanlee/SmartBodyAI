namespace SmartBodyAI.Models;

public class SmartChecklistSnapshotApplyResult
{
    public bool Success { get; set; }
    public int AppliedItemCount { get; set; }
    public List<string> Warnings { get; set; } = [];
}
