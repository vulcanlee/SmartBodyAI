namespace SmartBodyAI.Models;

public class SmartCallbackValidationResult
{
    public bool IsValid => Errors.Count == 0 && !string.IsNullOrWhiteSpace(State) && !string.IsNullOrWhiteSpace(Code);
    public string? Code { get; set; }
    public string? State { get; set; }
    public SmartAppSettingModel? StoredState { get; set; }
    public List<string> Errors { get; set; } = [];
}
