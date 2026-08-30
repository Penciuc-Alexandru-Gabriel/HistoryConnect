namespace HistoryConnect.Models;

public class IstoricRaspunsuri
{
    public int IdIstoric { get; set; }
    public int IdUtilizator { get; set; }
    public int IdProgresQ { get; set; }
    public int IdIntrebare { get; set; }

    public int? IdVarianta { get; set; }

    public Utilizator? Utilizator { get; set; }
    public ProgresQuiz? ProgresQuiz { get; set; }
    public Intrebare? Intrebare { get; set; }

    public VariantaRaspuns? VariantaRaspuns { get; set; }
}
