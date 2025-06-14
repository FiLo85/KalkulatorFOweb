using System.ComponentModel.DataAnnotations;

namespace KalkulatorFOweb.Models;

public class CalculationHistory
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public string Expression { get; set; }

    [Required]
    public string Result { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}