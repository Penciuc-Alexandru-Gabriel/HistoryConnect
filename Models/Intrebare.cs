using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public enum TipIntrebare
{
    Grila,
    AdevaratFals
}

public class Intrebare
{
    public int IdIntrebare { get; set; }
    public int IdQuiz { get; set; }

    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public TipIntrebare Tip { get; set; } = TipIntrebare.Grila;

    public int Timp { get; set; } = 5;

    [MaxLength(255)]
    public string? UrlImagine { get; set; }

    [Required]
[MaxLength(500)]
public string Feedback { get; set; } = string.Empty;

    public Quiz? Quiz { get; set; }
    public ICollection<VariantaRaspuns> Variante { get; set; } = new List<VariantaRaspuns>();
}
