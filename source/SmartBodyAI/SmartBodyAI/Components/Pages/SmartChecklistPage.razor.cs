using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SmartBodyAI.Models;
using SmartBodyAI.Services;

namespace SmartBodyAI.Components.Pages;

public partial class SmartChecklistPage
{
    private const string LocalStorageKey = "smart-checklist:draft";
    private const long MaxImportFileSize = 1024 * 1024;

    private static readonly HealthIndicatorStatus[] StatusOptions =
    [
        HealthIndicatorStatus.Green,
        HealthIndicatorStatus.Yellow,
        HealthIndicatorStatus.Red
    ];

    [Inject]
    public SmartChecklistTemplateService TemplateService { get; init; } = default!;

    [Inject]
    public SmartChecklistPersistenceService PersistenceService { get; init; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; init; } = default!;

    [Inject]
    public INotificationService Notice { get; init; } = default!;

    [Inject]
    public ILogger<SmartChecklistPage> Logger { get; init; } = default!;

    protected SmartChecklistPageModel Model { get; set; } = new();
    protected DateTimeOffset? LastSavedAt { get; set; }
    protected string PageMessage { get; set; } = string.Empty;
    protected NotificationType PageMessageType { get; set; } = NotificationType.Info;

    protected override void OnInitialized()
    {
        ResetModel();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await RestoreDraftAsync();
    }

    protected void UpdateStatus(SmartChecklistItem item, HealthIndicatorStatus status)
    {
        item.Status = status;
        Model.Recalculate();
        SetPageMessage("目前變更尚未儲存，可於完成一輪檢查後按下「儲存」。", NotificationType.Info);
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
        SetPageMessage("目前變更尚未儲存，可於完成一輪檢查後按下「儲存」。", NotificationType.Info);
    }

    protected async Task SaveAsync()
    {
        var snapshot = PersistenceService.CreateSnapshot(Model);
        var json = PersistenceService.Serialize(snapshot);

        await JSRuntime.InvokeVoidAsync("smartChecklist.saveToLocalStorage", LocalStorageKey, json);

        LastSavedAt = snapshot.ExportedAt;
        SetPageMessage("Checklist 已儲存到本機瀏覽器，可重新整理後續編輯。", NotificationType.Success);
        await OpenNoticeAsync("SMART Checklist", "Checklist 已儲存到本機草稿。", NotificationType.Success);
    }

    protected async Task ExportAsync()
    {
        var snapshot = PersistenceService.CreateSnapshot(Model);
        var json = PersistenceService.Serialize(snapshot);
        string filename = $"smart-checklist-{snapshot.ExportedAt:yyyyMMdd-HHmmss}.json";

        await JSRuntime.InvokeVoidAsync("smartChecklist.downloadJson", filename, json);

        SetPageMessage("JSON 快照已匯出，可用於交接或後續匯入。", NotificationType.Success);
        await OpenNoticeAsync("SMART Checklist", "JSON 快照已匯出。", NotificationType.Success);
    }

    protected async Task ResetAsync()
    {
        ResetModel();
        await JSRuntime.InvokeVoidAsync("smartChecklist.removeFromLocalStorage", LocalStorageKey);

        SetPageMessage("已重設為初始模板，並清除本機草稿。", NotificationType.Warning);
        await OpenNoticeAsync("SMART Checklist", "已重設 checklist 並清除本機草稿。", NotificationType.Warning);
    }

    protected async Task OnImportJsonAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(MaxImportFileSize);
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            var snapshot = PersistenceService.Deserialize(json);

            var importedModel = TemplateService.CreatePageModel();
            var result = PersistenceService.ApplySnapshot(importedModel, snapshot);

            Model = importedModel;
            LastSavedAt = snapshot.ExportedAt == default ? DateTimeOffset.Now : snapshot.ExportedAt;

            var normalizedJson = PersistenceService.Serialize(snapshot);
            await JSRuntime.InvokeVoidAsync("smartChecklist.saveToLocalStorage", LocalStorageKey, normalizedJson);

            string message = result.Warnings.Count == 0
                ? "匯入成功，頁面已還原為 JSON 內容。"
                : $"匯入成功，但有 {result.Warnings.Count} 個未知項目已忽略。";

            SetPageMessage(message, result.Warnings.Count == 0 ? NotificationType.Success : NotificationType.Warning);
            await OpenNoticeAsync("SMART Checklist", message, result.Warnings.Count == 0 ? NotificationType.Success : NotificationType.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "匯入 smart checklist JSON 失敗。");
            SetPageMessage($"匯入失敗：{ex.Message}", NotificationType.Error);
            await OpenNoticeAsync("SMART Checklist", $"匯入失敗：{ex.Message}", NotificationType.Error);
        }
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

    protected string GetMessageClass() => PageMessageType switch
    {
        NotificationType.Success => "message-success",
        NotificationType.Warning => "message-warning",
        NotificationType.Error => "message-error",
        _ => "message-info"
    };

    protected string GetLastSavedLabel()
        => LastSavedAt is null ? "尚未儲存" : $"最後儲存：{LastSavedAt:yyyy-MM-dd HH:mm:ss}";

    private void ResetModel()
    {
        Model = TemplateService.CreatePageModel();
        Model.Recalculate();
        LastSavedAt = null;
        PageMessage = "這是人工審查工具。完成一輪檢查後可儲存本機草稿，或匯出 JSON 供交接。";
        PageMessageType = NotificationType.Info;
    }

    private async Task RestoreDraftAsync()
    {
        try
        {
            string? json = await JSRuntime.InvokeAsync<string?>("smartChecklist.loadFromLocalStorage", LocalStorageKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var snapshot = PersistenceService.Deserialize(json);
            var restoredModel = TemplateService.CreatePageModel();
            var result = PersistenceService.ApplySnapshot(restoredModel, snapshot);

            Model = restoredModel;
            LastSavedAt = snapshot.ExportedAt == default ? null : snapshot.ExportedAt;

            string message = result.Warnings.Count == 0
                ? "已從本機草稿還原上次儲存的 checklist。"
                : $"已還原草稿，但忽略了 {result.Warnings.Count} 個無法對應的項目。";

            SetPageMessage(message, result.Warnings.Count == 0 ? NotificationType.Success : NotificationType.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "還原 smart checklist 本機草稿失敗。");
            ResetModel();
            SetPageMessage($"本機草稿還原失敗，已改用初始模板：{ex.Message}", NotificationType.Error);
        }
    }

    private void SetPageMessage(string message, NotificationType notificationType)
    {
        PageMessage = message;
        PageMessageType = notificationType;
    }

    private Task OpenNoticeAsync(string title, string description, NotificationType type)
    {
        return Notice.Open(new NotificationConfig
        {
            Message = title,
            Description = description,
            NotificationType = type,
            Duration = 3.5,
            Key = Guid.NewGuid().ToString()
        });
    }
}
