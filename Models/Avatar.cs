using System.ComponentModel.DataAnnotations;

namespace HistoryConnect.Models;

public class Avatar
{
    public int IdAvatar { get; set; }

    [Required]
    [MaxLength(100)]
    public string NumeAvatar { get; set; } = string.Empty;

   [Required]
  [MaxLength(255)]
    public string UrlPoza { get; set; } = string.Empty;

    public int NivelNecesar { get; set; } = 1;
}
