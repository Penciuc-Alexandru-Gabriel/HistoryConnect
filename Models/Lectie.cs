using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public enum TipLectie
{
    Istorie,
    Traditii
}

public class Lectie
{
    public int IdLectie { get; set; }
    public int IdCapitol { get; set; }

    [Required]
    [MaxLength(200)]
    public string Titlu { get; set; } = string.Empty;

    public TipLectie Tip { get; set; } = TipLectie.Istorie;

    public string? Continut { get; set; }

    [MaxLength(255)]
    public string? UrlImagine { get; set; }

    public int? AnEveniment { get; set; }
    public int Ordine { get; set; } = 1;

    public Capitol? Capitol { get; set; }
    public ICollection<Quiz> Quizuri { get; set; } = new List<Quiz>();
}
