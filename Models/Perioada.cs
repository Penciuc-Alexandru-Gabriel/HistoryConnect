using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public class Perioada
{
    public int IdPerioada { get; set; }

    [Required]
    [MaxLength(150)]
    public string Nume { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descriere { get; set; }

    public int? Inceput { get; set; }
    public int? Sfarsit { get; set; }

    [MaxLength(255)]
    public string? UrlImagine { get; set; }

    public ICollection<Capitol> Capitole { get; set; } = new List<Capitol>();
}
