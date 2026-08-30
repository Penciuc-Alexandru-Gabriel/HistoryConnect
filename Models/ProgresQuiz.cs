namespace HistoryConnect.Models;

public class ProgresQuiz
{
    public int IdProgresQ { get; set; }
    public int IdUtilizator { get; set; }
    public int IdQuiz { get; set; }
    public int Scor { get; set; } = 0;
    public bool Evaluat { get; set; } = false;
    public DateTime DataCompletare { get; set; }

    public int XpAcordat { get; set; } = 0;

    public Utilizator? Utilizator { get; set; }
    public Quiz? Quiz { get; set; }
}