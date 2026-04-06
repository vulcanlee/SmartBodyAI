using SmartBodyAI.Models;
using SmartBodyAI.Services;
using Xunit;

namespace SmartBodyAI.Tests;

public class SmartChecklistPersistenceServiceTests
{
    [Fact]
    public void CreateSnapshot_CapturesEditableFieldsByItemKey()
    {
        var templateService = new SmartChecklistTemplateService();
        var persistenceService = new SmartChecklistPersistenceService();
        var model = templateService.CreatePageModel();
        var item = model.Sections.SelectMany(section => section.Items).First();

        item.Status = HealthIndicatorStatus.Red;
        item.TestResult = "以 sandbox 手動驗證失敗";
        item.FailureReason = "authorization endpoint 缺值";
        item.ImprovementSuggestion = "補上 SMART metadata";

        var snapshot = persistenceService.CreateSnapshot(model, DateTimeOffset.Parse("2026-04-06T09:30:00+08:00"));

        Assert.Equal(SmartChecklistPersistenceService.CurrentSnapshotVersion, snapshot.Version);
        Assert.Equal(model.Title, snapshot.Title);
        Assert.Equal(1, snapshot.Items.Count(entry => entry.Key == item.Key));

        var snapshotItem = Assert.Single(snapshot.Items, entry => entry.Key == item.Key);
        Assert.Equal(HealthIndicatorStatus.Red, snapshotItem.Status);
        Assert.Equal("以 sandbox 手動驗證失敗", snapshotItem.TestResult);
        Assert.Equal("authorization endpoint 缺值", snapshotItem.FailureReason);
        Assert.Equal("補上 SMART metadata", snapshotItem.ImprovementSuggestion);
    }

    [Fact]
    public void ApplySnapshot_UpdatesKnownItemsAndIgnoresUnknownKeys()
    {
        var templateService = new SmartChecklistTemplateService();
        var persistenceService = new SmartChecklistPersistenceService();
        var model = templateService.CreatePageModel();
        var targetItem = model.Sections.SelectMany(section => section.Items).First();

        var snapshot = new SmartChecklistSnapshot
        {
            Version = SmartChecklistPersistenceService.CurrentSnapshotVersion,
            Items =
            [
                new SmartChecklistSnapshotItem
                {
                    Key = targetItem.Key,
                    Status = HealthIndicatorStatus.Green,
                    TestResult = "已完成",
                    FailureReason = string.Empty,
                    ImprovementSuggestion = "保持現狀"
                },
                new SmartChecklistSnapshotItem
                {
                    Key = "unknown-item",
                    Status = HealthIndicatorStatus.Red,
                    TestResult = "should be ignored",
                    FailureReason = "should be ignored",
                    ImprovementSuggestion = "should be ignored"
                }
            ]
        };

        var result = persistenceService.ApplySnapshot(model, snapshot);

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedItemCount);
        Assert.Single(result.Warnings);
        Assert.Contains("unknown-item", result.Warnings[0]);
        Assert.Equal(HealthIndicatorStatus.Green, targetItem.Status);
        Assert.Equal("已完成", targetItem.TestResult);
        Assert.Equal("保持現狀", targetItem.ImprovementSuggestion);
    }

    [Fact]
    public void Deserialize_RejectsUnsupportedSnapshotVersion()
    {
        var persistenceService = new SmartChecklistPersistenceService();
        const string json = """
            {
              "version": 999,
              "title": "SMART on FHIR Standalone Sandbox Readiness",
              "items": []
            }
            """;

        var exception = Record.Exception(() => persistenceService.Deserialize(json));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_PreservesReadableUtf8Characters()
    {
        var persistenceService = new SmartChecklistPersistenceService();
        var snapshot = new SmartChecklistSnapshot
        {
            Version = SmartChecklistPersistenceService.CurrentSnapshotVersion,
            Title = "SMART 檢查清單",
            Items =
            [
                new SmartChecklistSnapshotItem
                {
                    Key = "launch-entry",
                    Status = HealthIndicatorStatus.Green,
                    TestResult = "確認 sandbox 可直接開啟",
                    FailureReason = "尚未完成人工確認",
                    ImprovementSuggestion = "建立獨立的 SMART 啟動路由"
                }
            ]
        };

        var json = persistenceService.Serialize(snapshot);

        Assert.Contains("確認 sandbox 可直接開啟", json);
        Assert.DoesNotContain("\\u78BA\\u8A8D", json);
    }
}
