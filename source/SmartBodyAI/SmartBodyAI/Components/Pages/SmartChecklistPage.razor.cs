using Microsoft.AspNetCore.Components;
using SmartBodyAI.Models;
using SmartBodyAI.Services;

namespace SmartBodyAI.Components.Pages;

public partial class SmartChecklistPage
{
    private static readonly HealthIndicatorStatus[] StatusOptions =
    [
        HealthIndicatorStatus.Green,
        HealthIndicatorStatus.Yellow,
        HealthIndicatorStatus.Red
    ];

    [Inject]
    public SmartChecklistTemplateService TemplateService { get; init; } = default!;

    protected SmartChecklistPageModel Model { get; set; } = new();

    protected override void OnInitialized()
    {
        Model = TemplateService.CreatePageModel();
        Model.Recalculate();
    }

    protected void UpdateStatus(SmartChecklistItem item, HealthIndicatorStatus status)
    {
        item.Status = status;
        Model.Recalculate();
    }

    protected void UpdateText(SmartChecklistItem item, string fieldName, string? value)
    {
        string nextValue = value ?? string.Empty;

        switch (fieldName)
        {
            case nameof(SmartChecklistItem.TestResult):
                item.TestResult = nextValue;
                break;
            case nameof(SmartChecklistItem.FailureReason):
                item.FailureReason = nextValue;
                break;
            case nameof(SmartChecklistItem.ImprovementSuggestion):
                item.ImprovementSuggestion = nextValue;
                break;
        }

        Model.Recalculate();
    }

    protected static string GetStatusLabel(HealthIndicatorStatus status) => status switch
    {
        HealthIndicatorStatus.Green => "綠燈",
        HealthIndicatorStatus.Yellow => "黃燈",
        _ => "紅燈"
    };

    protected static string GetIndicatorClass(HealthIndicatorStatus status) => status switch
    {
        HealthIndicatorStatus.Green => "light-green",
        HealthIndicatorStatus.Yellow => "light-yellow",
        _ => "light-red"
    };

    protected static string GetIndicatorPillClass(HealthIndicatorStatus status) => status switch
    {
        HealthIndicatorStatus.Green => "status-green",
        HealthIndicatorStatus.Yellow => "status-yellow",
        _ => "status-red"
    };

    protected static string GetScoreClass(int score)
    {
        if (score >= 85) return "score-green";
        if (score >= 60) return "score-yellow";
        return "score-red";
    }
}
