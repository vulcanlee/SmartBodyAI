using Microsoft.AspNetCore.Components;
using SmartBodyAI.Models;
using SmartBodyAI.Services;

namespace SmartBodyAI.Components.Pages;

public partial class HealthCheckPage
{
    [SupplyParameterFromQuery(Name = "iss")]
    public string? Iss { get; set; }

    [SupplyParameterFromQuery(Name = "launch")]
    public string? Launch { get; set; }

    [SupplyParameterFromQuery(Name = "debug")]
    public bool? Debug { get; set; }

    [Inject]
    public HealthCheckService HealthCheckService { get; init; } = default!;

    [Inject]
    public ILogger<HealthCheckPage> Logger { get; init; } = default!;

    protected HealthCheckSummary? summary;
    protected bool isLoading = true;
    protected string? loadError;

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        loadError = null;

        try
        {
            summary = await HealthCheckService.GenerateAsync(Iss, Launch, Debug);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load health check summary.");
            loadError = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    protected static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value;

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
