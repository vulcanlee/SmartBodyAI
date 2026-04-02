namespace SmartBodyAI.Models;

public class HealthIndicatorResult
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public HealthIndicatorStatus Status { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? SourceValue { get; set; }
    public string? EffectiveValue { get; set; }
    public string? ProviderName { get; set; }

    public int Score => Status switch
    {
        HealthIndicatorStatus.Green => 100,
        HealthIndicatorStatus.Yellow => 50,
        _ => 0
    };
}
