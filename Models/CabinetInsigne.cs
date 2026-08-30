namespace HistoryConnect.Models;

public class CabinetInsigne
{
    public int IdUtilizator { get; set; }
    public int IdInsigna { get; set; }
    public DateTime DataObtinere { get; set; } = DateTime.UtcNow;

    public Utilizator? Utilizator { get; set; }
    public Insigna? Insigna { get; set; }
}
