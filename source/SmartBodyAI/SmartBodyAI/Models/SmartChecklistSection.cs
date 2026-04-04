namespace SmartBodyAI.Models;

public class SmartChecklistSection
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<SmartChecklistItem> Items { get; set; } = [];
    public int Score { get; set; }
    public int GreenCount { get; set; }
    public int YellowCount { get; set; }
    public int RedCount { get; set; }
}
