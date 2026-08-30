using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public enum TipConditieInsigna
{
    LectiiCompletate,      // nr. de lecții completate >= prag
    QuizuriCompletate,     // nr. de quizuri distincte completate >= prag
    ToateLectiile,         // toate lectiile din platformă completate (prag ignorat)
    XpAtins,               // XP total >= prag
    NivelAtins             // nivel curent >= prag
}

public class Insigna
{
    public int IdInsigna { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nume { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string ConditiiObtinere { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? UrlImagine { get; set; }

    public TipConditieInsigna TipConditie { get; set; }

    public int PragConditie { get; set; }
}