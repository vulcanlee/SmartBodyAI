namespace SmartBodyAI.Models;

public class SmartChecklistSnapshotItem
{
    public string Key { get; set; } = string.Empty;
    public HealthIndicatorStatus Status { get; set; }
    public string TestResult { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ImprovementSuggestion { get; set; } = string.Empty;
}
