using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public class Capitol
{
    public int IdCapitol { get; set; }
    public int IdPerioada { get; set; }

    [Required]
    [MaxLength(120)]
    public string Titlu { get; set; } = string.Empty;

    public int NrOrdine { get; set; } = 1;

    public Perioada? Perioada { get; set; }
    public ICollection<Lectie> Lectii { get; set; } = new List<Lectie>();
}
