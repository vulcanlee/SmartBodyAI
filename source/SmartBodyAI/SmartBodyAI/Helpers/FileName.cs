namespace SmartBodyAI.Helpers;

public static class StringExtensions
{
    public static float ToFloat(this string stringNumber)
    {
        if (stringNumber == null)
        {
            return 0;
        }

        bool isFloat = float.TryParse(stringNumber, out float result);
        if (isFloat)
        {
            string toFloat = result.ToString("F2");
            return float.Parse(toFloat);
        }
        else
        {
            return 0;
        }
    }
}
