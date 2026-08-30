namespace HistoryConnect.ViewModels.Quiz;

public class SaveResultRequest
{
    public int QuizId { get; set; }

    public List<RaspunsItem> Raspunsuri { get; set; } = new();
}

public class RaspunsItem
{
    public int IdIntrebare { get; set; }

    public  List<int> IdVariante { get; set; }= new();
}