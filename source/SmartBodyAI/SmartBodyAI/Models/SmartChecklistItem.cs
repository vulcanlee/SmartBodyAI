namespace SmartBodyAI.Models;

public class SmartChecklistItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int Weight { get; set; } = 1;
    public HealthIndicatorStatus Status { get; set; } = HealthIndicatorStatus.Yellow;
    public string WhyItMatters { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string TestResult { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ImprovementSuggestion { get; set; } = string.Empty;

    public int Score => Status switch
    {
        HealthIndicatorStatus.Green => 100,
        HealthIndicatorStatus.Yellow => 60,
        _ => 0
    };
}
