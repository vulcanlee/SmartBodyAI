using SmartBodyAI.Models;
using SmartBodyAI.Services;
using Xunit;

namespace SmartBodyAI.Tests;

public class SmartChecklistTemplateServiceTests
{
    [Fact]
    public void CreatePageModel_ContainsAllStandaloneSections()
    {
        var service = new SmartChecklistTemplateService();

        var model = service.CreatePageModel();

        Assert.Collection(
            model.Sections,
            section => Assert.Equal("啟動入口", section.Title),
            section => Assert.Equal("SMART Discovery", section.Title),
            section => Assert.Equal("Authorization Request", section.Title),
            section => Assert.Equal("Callback 驗證", section.Title),
            section => Assert.Equal("Token Exchange", section.Title),
            section => Assert.Equal("Token Response", section.Title),
            section => Assert.Equal("FHIR API 存取", section.Title),
            section => Assert.Equal("安全性與沙盒準備", section.Title));
    }

    [Fact]
    public void CreatePageModel_PopulatesGuidanceFieldsForEveryItem()
    {
        var service = new SmartChecklistTemplateService();

        var model = service.CreatePageModel();

        Assert.All(
            model.Sections.SelectMany(section => section.Items),
            item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.WhyItMatters));
                Assert.False(string.IsNullOrWhiteSpace(item.Rule));
                Assert.False(string.IsNullOrWhiteSpace(item.ImprovementSuggestion));
                Assert.True(item.Weight is 1 or 2);
            });
    }

    [Fact]
    public void CreatePageModel_UsesRequiredWeightForMandatoryItems()
    {
        var service = new SmartChecklistTemplateService();

        var model = service.CreatePageModel();

        var requiredItems = model.Sections
            .SelectMany(section => section.Items)
            .Where(item => item.IsRequired)
            .ToList();

        Assert.NotEmpty(requiredItems);
        Assert.All(requiredItems, item => Assert.Equal(2, item.Weight));
    }

    [Fact]
    public void Recalculate_ComputesWeightedScoreAndRiskSummary()
    {
        var model = new SmartChecklistPageModel
        {
            Sections =
            [
                new SmartChecklistSection
                {
                    Title = "測試分類",
                    Items =
                    [
                        new SmartChecklistItem
                        {
                            Key = "required-green",
                            Title = "必要綠燈",
                            IsRequired = true,
                            Weight = 2,
                            Status = HealthIndicatorStatus.Green
                        },
                        new SmartChecklistItem
                        {
                            Key = "optional-yellow",
                            Title = "一般黃燈",
                            IsRequired = false,
                            Weight = 1,
                            Status = HealthIndicatorStatus.Yellow
                        }
                    ]
                }
            ]
        };

        model.Recalculate();

        Assert.Equal(87, model.TotalScore);
        Assert.Equal("可望通過", model.OverallStatusLabel);
        Assert.Equal(1, model.GreenCount);
        Assert.Equal(1, model.YellowCount);
        Assert.Equal(0, model.RedCount);
        Assert.Single(model.KeyRisks);
        Assert.Equal(87, model.Sections[0].Score);
    }

    [Fact]
    public void Recalculate_UsesWarningStatusWhenRequiredItemIsYellow()
    {
        var model = new SmartChecklistPageModel
        {
            Sections =
            [
                new SmartChecklistSection
                {
                    Title = "測試分類",
                    Items =
                    [
                        new SmartChecklistItem
                        {
                            Key = "required-yellow",
                            Title = "必要黃燈",
                            IsRequired = true,
                            Weight = 2,
                            Status = HealthIndicatorStatus.Yellow,
                            FailureReason = "需要補人工確認"
                        },
                        new SmartChecklistItem
                        {
                            Key = "optional-green",
                            Title = "一般綠燈",
                            IsRequired = false,
                            Weight = 1,
                            Status = HealthIndicatorStatus.Green
                        }
                    ]
                }
            ]
        };

        model.Recalculate();

        Assert.Equal(73, model.TotalScore);
        Assert.Equal("有風險", model.OverallStatusLabel);
        Assert.Single(model.KeyRisks);
        Assert.Contains("必要黃燈", model.KeyRisks[0]);
    }

    [Fact]
    public void Recalculate_UsesRedStatusWhenRequiredItemFails()
    {
        var model = new SmartChecklistPageModel
        {
            Sections =
            [
                new SmartChecklistSection
                {
                    Title = "測試分類",
                    Items =
                    [
                        new SmartChecklistItem
                        {
                            Key = "required-red",
                            Title = "必要紅燈",
                            IsRequired = true,
                            Weight = 2,
                            Status = HealthIndicatorStatus.Red,
                            FailureReason = "缺少必要規格"
                        },
                        new SmartChecklistItem
                        {
                            Key = "optional-green",
                            Title = "一般綠燈",
                            IsRequired = false,
                            Weight = 1,
                            Status = HealthIndicatorStatus.Green
                        }
                    ]
                }
            ]
        };

        model.Recalculate();

        Assert.Equal(33, model.TotalScore);
        Assert.Equal("目前難以通過", model.OverallStatusLabel);
        Assert.Single(model.KeyRisks);
        Assert.Contains("必要紅燈", model.KeyRisks[0]);
    }
}
