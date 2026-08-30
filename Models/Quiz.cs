using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public class Quiz
{
    public int IdQuiz { get; set; }
    public int IdLectie { get; set; }

    [Required]
    [MaxLength(200)]
    public string Titlu { get; set; } = string.Empty;

    public int XpCompletare { get; set; } = 0;
    public int Timp { get; set; } = 100;

    [MaxLength(500)]
    public string? Feedback { get; set; }

    public Lectie? Lectie { get; set; }
    public ICollection<Intrebare> Intrebari { get; set; } = new List<Intrebare>();
}
