using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace HistoryConnect.Models;

public class Utilizator : IdentityUser<int>
{
    [Required]
    [MaxLength(100)]
    public string Nume { get; set; } = string.Empty;

    public int? IdAvatar { get; set; }

    public DateTime DataInregistrare { get; set; } = DateTime.UtcNow;

    public Avatar? Avatar { get; set; }
    public Student? Student { get; set; }
    public Administrator? Administrator { get; set; }
}
