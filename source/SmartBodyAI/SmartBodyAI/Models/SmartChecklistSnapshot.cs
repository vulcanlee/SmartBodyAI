namespace SmartBodyAI.Models;

public class SmartChecklistSnapshot
{
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; }
    public List<SmartChecklistSnapshotItem> Items { get; set; } = [];
}
