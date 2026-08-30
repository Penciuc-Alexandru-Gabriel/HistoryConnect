namespace HistoryConnect.Models;

public class Administrator
{
    public int IdUtilizator { get; set; }

    public DateTime DataNumire { get; set; } = DateTime.UtcNow;

    public Utilizator? Utilizator { get; set; }
}
