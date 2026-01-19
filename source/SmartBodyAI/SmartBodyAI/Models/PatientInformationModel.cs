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

    public void Reset()
    {
        Id = string.Empty;
        Identifier = string.Empty;
        Name = string.Empty;
        BirthDate = string.Empty;
        Gender = string.Empty;
        HeightValue = string.Empty;
        HeightUnit = string.Empty;
        WeightValue = string.Empty;
        WeightUnit = string.Empty;
    }

    public string GetAge()
    {
        if (DateTime.TryParse(BirthDate, out var birthDate))
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age.ToString()+ " 歲";
        }
        return "";
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
