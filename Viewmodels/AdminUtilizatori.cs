using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.ViewModels.AdminUtilizatori;

public class UtilizatorViewModel
{
    public int Id { get; set; }
    public string Nume { get; set; } = "";
    public string NumeUtilizator { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rol { get; set; } = "";
    public int XpTotal { get; set; }
    public int NivelCurent { get; set; }
    public DateTime DataInregistrare { get; set; }
    public DateTime? DataNumire { get; set; }
    public int? AprobarePromovareInitiataDeId { get; set; }
    public string? AprobarePromovareInitiataDe { get; set; }
    public DateTime? AprobarePromovareData { get; set; }
    public int? AprobareStergereInitiataDeId { get; set; }
    public string? AprobareStergereInitiataDe { get; set; }
    public DateTime? AprobareStergereData { get; set; }
    public bool AreAprobarePromovareInAsteptare => AprobarePromovareInitiataDeId.HasValue;
    public bool AreAprobareStergereInAsteptare => AprobareStergereInitiataDeId.HasValue;
}

public class ResetareParolaInput
{
    [Required(ErrorMessage = "Id-ul utilizatorului lipsește.")]
    public int IdUtilizator { get; set; }

    [Required(ErrorMessage = "Parola nouă este obligatorie.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Parola trebuie să aibă minimum 6 caractere.")]
    [DataType(DataType.Password)]
    public string ParolaNoua { get; set; } = string.Empty;
}
