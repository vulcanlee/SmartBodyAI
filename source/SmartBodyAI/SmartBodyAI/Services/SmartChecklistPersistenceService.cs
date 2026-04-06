using System.Text.Encodings.Web;
using System.Text.Json;
using SmartBodyAI.Models;

namespace SmartBodyAI.Services;

public class SmartChecklistPersistenceService
{
    public const int CurrentSnapshotVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public SmartChecklistSnapshot CreateSnapshot(
        SmartChecklistPageModel model,
        DateTimeOffset? exportedAt = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SmartChecklistSnapshot
        {
            Version = CurrentSnapshotVersion,
            Title = model.Title,
            ExportedAt = exportedAt ?? DateTimeOffset.Now,
            Items = model.Sections
                .SelectMany(section => section.Items)
                .Select(item => new SmartChecklistSnapshotItem
                {
                    Key = item.Key,
                    Status = item.Status,
                    TestResult = item.TestResult,
                    FailureReason = item.FailureReason,
                    ImprovementSuggestion = item.ImprovementSuggestion
                })
                .ToList()
        };
    }

    public SmartChecklistSnapshotApplyResult ApplySnapshot(
        SmartChecklistPageModel model,
        SmartChecklistSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(snapshot);

        var result = new SmartChecklistSnapshotApplyResult
        {
            Success = true
        };

        var itemsByKey = model.Sections
            .SelectMany(section => section.Items)
            .ToDictionary(item => item.Key, StringComparer.Ordinal);

        foreach (var snapshotItem in snapshot.Items)
        {
            if (!itemsByKey.TryGetValue(snapshotItem.Key, out var existingItem))
            {
                result.Warnings.Add($"找不到對應的 checklist 項目 key：{snapshotItem.Key}");
                continue;
            }

            existingItem.Status = snapshotItem.Status;
            existingItem.TestResult = snapshotItem.TestResult ?? string.Empty;
            existingItem.FailureReason = snapshotItem.FailureReason ?? string.Empty;
            existingItem.ImprovementSuggestion = snapshotItem.ImprovementSuggestion ?? string.Empty;
            result.AppliedItemCount++;
        }

        model.Recalculate();
        return result;
    }

    public string Serialize(SmartChecklistSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public SmartChecklistSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("Checklist JSON 內容不可為空白。");
        }

        SmartChecklistSnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize<SmartChecklistSnapshot>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Checklist JSON 格式錯誤，無法解析。", ex);
        }

        if (snapshot is null)
        {
            throw new InvalidOperationException("Checklist JSON 內容為空，無法還原。");
        }

        if (snapshot.Version != CurrentSnapshotVersion)
        {
            throw new InvalidOperationException($"Checklist snapshot version 不支援：{snapshot.Version}。");
        }

        snapshot.Title ??= string.Empty;
        snapshot.Items ??= [];

        return snapshot;
    }
}
