namespace HistoryConnect.Models;

public class Student
{
    public int IdUtilizator { get; set; }
    public int XpTotal { get; set; } = 0;
    public int NivelCurent { get; set; } = 1;

    public Utilizator? Utilizator { get; set; }
}
