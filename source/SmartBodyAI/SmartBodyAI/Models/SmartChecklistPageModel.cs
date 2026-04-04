namespace SmartBodyAI.Models;

public class SmartChecklistPageModel
{
    public string Title { get; set; } = "SMART on FHIR Standalone Sandbox Readiness";
    public string Subtitle { get; set; } = string.Empty;
    public List<SmartChecklistSection> Sections { get; set; } = [];
    public int TotalScore { get; set; }
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
    public string OverallStatusLabel { get; set; } = "有風險";
    public List<string> KeyRisks { get; set; } = [];
    public List<string> FinalRecommendations { get; set; } = [];

    public void Recalculate()
    {
        var allItems = Sections.SelectMany(section => section.Items).ToList();
        var totalWeight = allItems.Sum(item => Math.Max(1, item.Weight));
        var weightedScore = allItems.Sum(item => item.Score * Math.Max(1, item.Weight));
        var hasRequiredRed = allItems.Any(item => item.IsRequired && item.Status == HealthIndicatorStatus.Red);
        var hasRequiredYellow = allItems.Any(item => item.IsRequired && item.Status == HealthIndicatorStatus.Yellow);

        foreach (var section in Sections)
        {
            var sectionWeight = section.Items.Sum(item => Math.Max(1, item.Weight));
            var sectionScore = section.Items.Sum(item => item.Score * Math.Max(1, item.Weight));
            section.Score = sectionWeight == 0
                ? 0
                : (int)Math.Round((double)sectionScore / sectionWeight, MidpointRounding.AwayFromZero);
            section.GreenCount = section.Items.Count(item => item.Status == HealthIndicatorStatus.Green);
            section.YellowCount = section.Items.Count(item => item.Status == HealthIndicatorStatus.Yellow);
            section.RedCount = section.Items.Count(item => item.Status == HealthIndicatorStatus.Red);
        }

        TotalScore = totalWeight == 0
            ? 0
            : (int)Math.Round((double)weightedScore / totalWeight, MidpointRounding.AwayFromZero);
        GreenCount = allItems.Count(item => item.Status == HealthIndicatorStatus.Green);
        YellowCount = allItems.Count(item => item.Status == HealthIndicatorStatus.Yellow);
        RedCount = allItems.Count(item => item.Status == HealthIndicatorStatus.Red);

        OverallStatusLabel = hasRequiredRed || TotalScore < 60
            ? "目前難以通過"
            : TotalScore >= 85 && !hasRequiredYellow
                ? "可望通過"
                : "有風險";

        KeyRisks = allItems
            .Where(item => item.Status == HealthIndicatorStatus.Red
                || (item.IsRequired && item.Status == HealthIndicatorStatus.Yellow))
            .Select(item =>
            {
                var reason = string.IsNullOrWhiteSpace(item.FailureReason)
                    ? item.ImprovementSuggestion
                    : item.FailureReason;
                return $"{item.Title}: {reason}";
            })
            .Take(5)
            .ToList();

        if (KeyRisks.Count == 0)
        {
            KeyRisks.Add("目前沒有紅燈或必要黃燈項目，系統已接近 sandbox 測試門檻。");
        }

        FinalRecommendations = allItems
            .Where(item => item.Status != HealthIndicatorStatus.Green)
            .Select(item => $"{item.Title}: {item.ImprovementSuggestion}")
            .Distinct()
            .Take(6)
            .ToList();

        if (FinalRecommendations.Count == 0)
        {
            FinalRecommendations.Add("建議進行一次完整 sandbox 流程驗證，確認人工評估與實際行為一致。");
        }
    }
}
