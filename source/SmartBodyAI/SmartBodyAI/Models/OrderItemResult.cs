namespace SmartBodyAI.Models;

public class OrderItemResult
{
    public string EncounterId { get; set; }
    public string EncounterType { get; set; }
    public DateTimeOffset? EncounterStart { get; set; }
    public string OrderResourceType { get; set; }
    public string OrderId { get; set; }
    public string OrderCode { get; set; }
    public string OrderDisplay { get; set; }
}
