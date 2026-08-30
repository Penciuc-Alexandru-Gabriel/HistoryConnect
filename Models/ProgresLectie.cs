namespace HistoryConnect.Models;

public class ProgresLectie
{
    public int IdProgresL { get; set; }
    public int IdUtilizator { get; set; }
    public int IdLectie { get; set; }
    public bool Completata { get; set; } = false;
    public DateTime? DataCompletare { get; set; }

    public Utilizator? Utilizator { get; set; }
    public Lectie? Lectie { get; set; }
}
