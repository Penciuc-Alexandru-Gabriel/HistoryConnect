namespace HistoryConnect.ViewModels.Clasament;

public class EntryClasament
{
    public int    Pozitie        { get; set; }
    public int    IdUtilizator   { get; set; }
    public string Nume           { get; set; } = "";
    public string Initiala       { get; set; } = "";
    public int    XpTotal        { get; set; }
    public int    NivelCurent    { get; set; }
    public string ValoareAfisata { get; set; } = "";
}