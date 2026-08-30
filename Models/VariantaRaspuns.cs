using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public class VariantaRaspuns
{
    public int IdVarianta { get; set; }
    public int IdIntrebare { get; set; }

    [Required]
    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    public bool Corect { get; set; } = false;
    public int Punctaj { get; set; } = 0;

    public Intrebare? Intrebare { get; set; }
}
