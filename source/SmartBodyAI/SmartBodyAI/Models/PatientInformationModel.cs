namespace SmartBodyAI.Models;

public class PatientInformationModel
{
    public string Id { get; set; }
    public string Identifier { get; set; }
    public string Name { get; set; }
    public string BirthDate { get; set; }
    public string Gender { get; set; }
    public string HeightValue { get; set; }
    public string HeightUnit { get; set; }
    public string WeightValue { get; set; }
    public string WeightUnit { get; set; }

    public string GetAge()
    {
        if (DateTime.TryParse(BirthDate, out var birthDate))
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age.ToString();
        }
        return "未知";
    }

    public string GetHeight()
    {
        return $"{HeightValue} {HeightUnit}";
    }
    public string GetWeight()
    {
        return $"{WeightValue} {WeightUnit}";
    }
}
