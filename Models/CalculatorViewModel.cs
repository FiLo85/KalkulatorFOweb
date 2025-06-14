namespace KalkulatorFOweb.Models;

public class CalculatorViewModel
{
    public string Display { get; set; } = "0";
    public List<CalculationHistory> History { get; set; } = new List<CalculationHistory>();
}